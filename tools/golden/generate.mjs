// Writes the synthetic documents of the golden scenarios (docs/10-code-structure.md, section 10).
//   node tools/golden/generate.mjs              writes them under fixtures/golden
//   node tools/golden/generate.mjs --out <dir>  writes the same tree under <dir>, to compare two runs
//
// PDF and JPG files are HTML rendered by the Chromium of @playwright/test (`npx playwright install chromium` once).
// DOCX and XLSX files are built here as ZIP archives of stored entries with the smallest OOXML parts Word and
// LibreOffice open. Every date inside the files is fixed, so two runs on one machine give the same bytes. Text is
// drawn with the machine's fonts, so another operating system or Chromium build may give other bytes.
// All content is synthetic: the golden case of docs/06-data-model.md, section 9.
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { crc32 } from 'node:zlib';
import { chromium } from '@playwright/test';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const outArg = process.argv.indexOf('--out');
const outRoot = outArg > 0 ? resolve(process.argv[outArg + 1]) : join(root, 'fixtures', 'golden');

// ---------------------------------------------------------------------------------------------------------------
// The golden case
// ---------------------------------------------------------------------------------------------------------------

const golden = {
  member: {
    name: 'Nguyễn Văn An',
    code: 'MB-0007731',
    nationalId: '000000000001', // db/seed/dev/040_members_and_policies.sql
    birthDate: '12/04/1988',
    gender: 'Nam',
    address: 'Số 8 ngõ 12 phố Mẫu Thử, phường Ba Đình, Hà Nội',
  },
  policy: 'PL-2026-00731',
  provider: {
    name: 'Phòng khám Đa khoa Hồng Ngọc',
    taxCode: '0101234567',
    address: 'Số 25 phố Mẫu Thử, phường Ba Đình, Hà Nội',
    phone: '024 0000 0000',
  },
  invoice: { number: 'HN-0045821', series: '1C26THN', lookupCode: 'GOLDEN-CLEAN-0045821' },
  serviceDate: { day: '12', month: '10', year: '2026' },
  prescriptionDate: { day: '13', month: '10', year: '2026' },
  diagnosis: { code: 'J02.9', name: 'Viêm họng cấp, không đặc hiệu' },
  doctor: 'BS. Phạm Minh Đức',
  cashier: 'Đỗ Thị Mai',
  lines: [
    { no: 1, text: 'Khám nội tổng quát', unit: 'Lần', qty: 1, price: 300000 },
    { no: 2, text: 'Xét nghiệm công thức máu', unit: 'Lần', qty: 1, price: 250000 },
    { no: 3, text: 'Amoxicillin 500mg × 20 viên', unit: 'Viên', qty: 20, price: 7500 },
    { no: 4, text: 'Kem dưỡng phục hồi da Obagi', unit: 'Tuýp', qty: 1, price: 300000 },
    { no: 5, text: 'Siêu âm ổ bụng', unit: 'Lần', qty: 1, price: 400000 },
  ],
  total: 1400000,
  totalInWords: 'Một triệu bốn trăm nghìn đồng chẵn',
};

for (const line of golden.lines) line.amount = line.qty * line.price;
if (golden.lines.reduce((sum, line) => sum + line.amount, 0) !== golden.total) {
  throw new Error('The golden lines no longer add up to the golden total.');
}

const SYNTHETIC = 'Tài liệu tổng hợp cho kiểm thử ClaimLens, không có giá trị pháp lý.';
const slash = ({ day, month, year }) => `${day}/${month}/${year}`;
const longDate = ({ day, month, year }) => `Ngày ${day} tháng ${month} năm ${year}`;
const vnd = (amount) => String(amount).replace(/\B(?=(\d{3})+(?!\d))/g, '.');
const escape = (text) =>
  String(text).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&apos;' })[c]);

// ---------------------------------------------------------------------------------------------------------------
// HTML templates
// ---------------------------------------------------------------------------------------------------------------

const baseCss = `
  * { box-sizing: border-box; }
  html, body { margin: 0; }
  body { font-family: Arial, "Liberation Sans", "DejaVu Sans", sans-serif; color: #1c1c1c; font-size: 12.5px; line-height: 1.45; }
  table { border-collapse: collapse; }
  .num { text-align: right; white-space: nowrap; font-variant-numeric: tabular-nums; }
  .center { text-align: center; }
  .muted { color: #555; }
`;

const a4Css = `
  @page { size: A4; margin: 14mm; }
  .page { position: relative; height: 266mm; break-after: page; }
  .page:last-child { break-after: auto; }
  .letterhead { display: flex; justify-content: space-between; align-items: flex-end; border-bottom: 2px solid #8b1d1d; padding-bottom: 8px; }
  .brand { font-size: 17px; font-weight: 700; color: #8b1d1d; text-transform: uppercase; }
  h1 { font-size: 20px; text-align: center; margin: 18px 0 2px; letter-spacing: 0.4px; }
  .sub { text-align: center; font-style: italic; margin: 0; }
  .kv { width: 100%; margin-top: 14px; }
  .kv td { padding: 3px 0; vertical-align: top; }
  .kv td.k { width: 34%; color: #444; }
  .grid { width: 100%; margin-top: 14px; }
  .grid th, .grid td { border: 1px solid #555; padding: 6px 8px; vertical-align: top; }
  .grid th { background: #efe9df; font-weight: 700; }
  .grid tr.index td { text-align: center; font-style: italic; color: #555; padding: 2px; font-size: 11px; }
  .signatures { display: flex; justify-content: space-around; margin-top: 28px; text-align: center; }
  .signatures .role { font-weight: 700; }
  .signatures .hint { font-style: italic; color: #555; font-size: 11px; }
  .signatures .name { margin-top: 64px; font-weight: 700; }
  footer { position: absolute; left: 0; right: 0; bottom: 0; display: flex; justify-content: space-between; font-size: 10.5px; color: #555; border-top: 1px solid #bbb; padding-top: 6px; }
`;

function letterhead() {
  const { provider } = golden;
  return `
    <div class="letterhead">
      <div>
        <div class="brand">${escape(provider.name)}</div>
        <div>Địa chỉ: ${escape(provider.address)}</div>
        <div>Mã số thuế: ${provider.taxCode} · Điện thoại: ${provider.phone}</div>
      </div>
    </div>`;
}

function invoiceHtml() {
  const { member, invoice, lines } = golden;
  const rows = lines
    .map(
      (line) => `
      <tr>
        <td class="center">${line.no}</td>
        <td>${escape(line.text)}</td>
        <td class="center">${escape(line.unit)}</td>
        <td class="num">${line.qty}</td>
        <td class="num">${vnd(line.price)}</td>
        <td class="num">${vnd(line.amount)}</td>
      </tr>`,
    )
    .join('');
  const invoiceMeta = `Ký hiệu: <b>${invoice.series}</b><br>Số: <b>${invoice.number}</b>`;
  return `<!doctype html><html lang="vi"><head><meta charset="utf-8">
    <title>Hóa đơn ${invoice.number}</title>
    <style>${baseCss}${a4Css}
      .title-block { position: relative; }
      .invoice-meta { position: absolute; right: 0; top: 0; text-align: right; line-height: 1.6; }
      .totals { width: 60%; margin-left: auto; margin-top: 14px; }
      .totals td { border: 1px solid #555; padding: 7px 10px; }
      .totals tr.grand td { font-weight: 700; font-size: 14px; background: #efe9df; }
      .words { margin-top: 14px; }
      .esign { display: inline-block; border: 1.5px solid #2f7d32; color: #2f7d32; padding: 6px 10px; margin-top: 10px; font-size: 11px; text-align: left; }
    </style></head><body>
    <section class="page">
      ${letterhead()}
      <div class="title-block">
        <div class="invoice-meta">${invoiceMeta}</div>
        <h1>HÓA ĐƠN GIÁ TRỊ GIA TĂNG</h1>
        <p class="sub">(Bản thể hiện của hóa đơn điện tử)</p>
        <p class="sub">${longDate(golden.serviceDate)}</p>
      </div>
      <table class="kv">
        <tr><td class="k">Họ tên người mua hàng:</td><td><b>${escape(member.name)}</b></td></tr>
        <tr><td class="k">Mã thành viên bảo hiểm:</td><td>${member.code}</td></tr>
        <tr><td class="k">Số hợp đồng bảo hiểm:</td><td>${golden.policy}</td></tr>
        <tr><td class="k">Địa chỉ:</td><td>${escape(member.address)}</td></tr>
        <tr><td class="k">Hình thức thanh toán:</td><td>TM/CK</td></tr>
        <tr><td class="k">Đồng tiền thanh toán:</td><td>VNĐ</td></tr>
      </table>
      <table class="grid">
        <thead>
          <tr>
            <th style="width: 7%">STT</th><th>Tên hàng hóa, dịch vụ</th><th style="width: 11%">Đơn vị tính</th>
            <th style="width: 10%">Số lượng</th><th style="width: 14%">Đơn giá</th><th style="width: 16%">Thành tiền</th>
          </tr>
          <tr class="index"><td>1</td><td>2</td><td>3</td><td>4</td><td>5</td><td>6 = 4 × 5</td></tr>
        </thead>
        <tbody>${rows}</tbody>
      </table>
      <p class="muted" style="margin-top: 10px">Dịch vụ khám, chữa bệnh thuộc đối tượng không chịu thuế GTGT (KCT).</p>
      <footer><span>Hóa đơn ${invoice.number} · ${escape(SYNTHETIC)}</span><span>Trang 1/2 · Xem tiếp trang 2</span></footer>
    </section>
    <section class="page">
      ${letterhead()}
      <h1 style="font-size: 16px">HÓA ĐƠN GIÁ TRỊ GIA TĂNG (tiếp theo)</h1>
      <p class="sub">Ký hiệu ${invoice.series} · Số ${invoice.number} · ${longDate(golden.serviceDate)}</p>
      <table class="totals">
        <tr><td>Cộng tiền hàng, dịch vụ</td><td class="num">${vnd(golden.total)}</td></tr>
        <tr><td>Thuế suất GTGT</td><td class="num">KCT</td></tr>
        <tr><td>Tiền thuế GTGT</td><td class="num">0</td></tr>
        <tr class="grand"><td>Tổng tiền thanh toán</td><td class="num">${vnd(golden.total)}</td></tr>
      </table>
      <p class="words">Số tiền viết bằng chữ: <b><i>${escape(golden.totalInWords)}.</i></b></p>
      <div class="signatures">
        <div>
          <div class="role">Người mua hàng</div>
          <div class="hint">(Ký, ghi rõ họ tên)</div>
          <div class="name">${escape(member.name)}</div>
        </div>
        <div>
          <div class="role">Người bán hàng</div>
          <div class="hint">(Ký, đóng dấu, ghi rõ họ tên)</div>
          <div class="esign">Ký bởi: ${escape(golden.provider.name.toUpperCase())}<br>Ký ngày: ${slash(golden.serviceDate)}</div>
        </div>
      </div>
      <p style="margin-top: 36px">Mã tra cứu hóa đơn: <b>${invoice.lookupCode}</b></p>
      <p class="muted">Cần kiểm tra, đối chiếu khi lập, giao, nhận hóa đơn.</p>
      <footer><span>Hóa đơn ${invoice.number} · ${escape(SYNTHETIC)}</span><span>Trang 2/2</span></footer>
    </section>
  </body></html>`;
}

function visitSummaryHtml() {
  const { member, diagnosis } = golden;
  return `<!doctype html><html lang="vi"><head><meta charset="utf-8">
    <title>Tóm tắt khám bệnh ${member.code}</title>
    <style>${baseCss}${a4Css}
      h2 { font-size: 13.5px; margin: 18px 0 6px; padding-bottom: 3px; border-bottom: 1px solid #ccc; }
      ul { margin: 4px 0; padding-left: 20px; }
    </style></head><body>
    <section class="page">
      ${letterhead()}
      <h1>TÓM TẮT KHÁM BỆNH NGOẠI TRÚ</h1>
      <p class="sub">Ngày khám: ${slash(golden.serviceDate)}</p>
      <table class="kv">
        <tr><td class="k">Họ tên người bệnh:</td><td><b>${escape(member.name)}</b></td></tr>
        <tr><td class="k">Ngày sinh · Giới tính:</td><td>${member.birthDate} · ${member.gender}</td></tr>
        <tr><td class="k">Số CCCD:</td><td>${member.nationalId}</td></tr>
        <tr><td class="k">Mã thành viên · Hợp đồng:</td><td>${member.code} · ${golden.policy}</td></tr>
        <tr><td class="k">Địa chỉ:</td><td>${escape(member.address)}</td></tr>
      </table>
      <h2>Lý do khám</h2>
      <p>Đau họng, nuốt vướng, sốt nhẹ 37,8°C từ hai ngày trước.</p>
      <h2>Khám lâm sàng</h2>
      <p>Họng đỏ, amidan hai bên không sưng, không có giả mạc. Hạch dưới hàm không sờ thấy. Phổi thông khí đều, không ran. Bụng mềm, không đau.</p>
      <h2>Cận lâm sàng đã làm</h2>
      <ul>
        <li>Xét nghiệm công thức máu: bạch cầu 10,8 G/L, bạch cầu trung tính tăng nhẹ.</li>
        <li>Siêu âm ổ bụng: chưa phát hiện bất thường.</li>
      </ul>
      <h2>Chẩn đoán</h2>
      <p><b>${diagnosis.code}</b> – ${escape(diagnosis.name)}</p>
      <h2>Điều trị</h2>
      <p>Kê đơn Amoxicillin 500mg, 20 viên, uống 1 viên/lần, ngày 2 lần, trong 10 ngày. Súc họng nước muối ấm, uống đủ nước.</p>
      <h2>Hẹn tái khám</h2>
      <p>Tái khám sau 10 ngày hoặc ngay khi sốt trên 38,5°C, khó thở.</p>
      <div class="signatures" style="justify-content: flex-end">
        <div>
          <div class="sub">${longDate(golden.serviceDate)}</div>
          <div class="role">Bác sĩ khám bệnh</div>
          <div class="hint">(Ký, ghi rõ họ tên)</div>
          <div class="name">${escape(golden.doctor)}</div>
        </div>
      </div>
      <footer><span>${escape(SYNTHETIC)}</span><span>Trang 1/1</span></footer>
    </section>
  </body></html>`;
}

// One page of a cash receipt, photographed on a desk. `rotate` stores it turned 90° counter-clockwise, as a phone
// held upright writes it, so EXIF Orientation 6 (rotate 90° clockwise to view) brings it back.
const RECEIPT = { width: 760, height: 1080 };

function receiptHtml({ blur = 0, rotate = false } = {}) {
  const { member, provider, invoice } = golden;
  const frame = rotate
    ? `width: ${RECEIPT.height}px; height: ${RECEIPT.width}px;`
    : `width: ${RECEIPT.width}px; height: ${RECEIPT.height}px;`;
  const turn = rotate ? `transform-origin: 0 0; transform: translateY(${RECEIPT.width}px) rotate(-90deg);` : '';
  return `<!doctype html><html lang="vi"><head><meta charset="utf-8">
    <title>Phiếu thu</title>
    <style>${baseCss}
      #shot { ${frame} overflow: hidden; position: relative; }
      .desk { width: ${RECEIPT.width}px; height: ${RECEIPT.height}px; ${turn}
        background: linear-gradient(160deg, #8d7b66 0%, #7a6956 55%, #6c5c4b 100%); padding: 40px 36px; }
      .paper { width: 100%; height: 100%; background: #fbfaf5; box-shadow: 0 6px 18px rgba(0, 0, 0, 0.35);
        padding: 34px 38px; font-size: 17px; line-height: 1.5; position: relative; filter: blur(${blur}px); }
      .brand { font-weight: 700; font-size: 19px; color: #8b1d1d; text-transform: uppercase; }
      .small { font-size: 14px; color: #444; }
      h1 { text-align: center; font-size: 30px; margin: 26px 0 0; letter-spacing: 2px; }
      .date { text-align: center; font-style: italic; margin-bottom: 10px; }
      .no { position: absolute; right: 38px; top: 34px; text-align: right; font-size: 15px; }
      .row { margin: 9px 0; }
      .amount { font-size: 22px; font-weight: 700; }
      .signs { display: flex; justify-content: space-between; text-align: center; margin-top: 26px; font-size: 15px; }
      .signs .role { font-weight: 700; }
      .signs .name { margin-top: 70px; font-weight: 700; }
      .stamp { position: absolute; right: 230px; top: 540px; border: 3px solid #b3261e; color: #b3261e; border-radius: 8px;
        padding: 6px 14px; font-weight: 700; font-size: 20px; transform: rotate(-12deg); opacity: 0.85; letter-spacing: 1px; }
      .foot { position: absolute; left: 38px; right: 38px; bottom: 22px; font-size: 12px; color: #666; text-align: center; }
    </style></head><body>
    <div id="shot"><div class="desk"><div class="paper">
      <div class="brand">${escape(provider.name)}</div>
      <div class="small">${escape(provider.address)}</div>
      <div class="small">Mã số thuế: ${provider.taxCode}</div>
      <div class="no">Quyển số: 07<br>Số: PT-2026-104582</div>
      <h1>PHIẾU THU</h1>
      <div class="date">${longDate(golden.serviceDate)}</div>
      <div class="row">Họ và tên người nộp tiền: <b>${escape(member.name)}</b></div>
      <div class="row">Địa chỉ: ${escape(member.address)}</div>
      <div class="row">Lý do nộp: Thanh toán hóa đơn số ${invoice.number}</div>
      <div class="row">Số tiền: <span class="amount">${vnd(golden.total)} đồng</span></div>
      <div class="row">Viết bằng chữ: <i>${escape(golden.totalInWords)}</i></div>
      <div class="row">Hình thức thanh toán: Tiền mặt</div>
      <div class="row">Kèm theo: 01 hóa đơn giá trị gia tăng</div>
      <div class="signs">
        <div><div class="role">Người nộp tiền</div><div class="name">${escape(member.name)}</div></div>
        <div><div class="role">Thủ quỹ</div><div class="name">${escape(golden.cashier)}</div></div>
      </div>
      <div class="stamp">ĐÃ THU TIỀN</div>
      <div class="foot">${escape(SYNTHETIC)}</div>
    </div></div></div>
  </body></html>`;
}

// ---------------------------------------------------------------------------------------------------------------
// Chromium rendering
// ---------------------------------------------------------------------------------------------------------------

// Chromium stamps the time of printing into the PDF information dictionary. Put a fixed date in its place, padded with
// spaces to the same length, so every byte offset in the cross-reference table stays right.
const PDF_DATE = '(D:20261012090000Z)';

function fixPdfDates(pdf) {
  const text = pdf.toString('latin1');
  let count = 0;
  const fixed = text.replace(/\/(CreationDate|ModDate) \(D:[^)]*\)/g, (match, key) => {
    count += 1;
    const replacement = `/${key} ${PDF_DATE}`;
    if (replacement.length > match.length) throw new Error(`Unexpected PDF date: ${match}`);
    return replacement.padEnd(match.length, ' ');
  });
  if (count === 0) throw new Error('No PDF dates found to fix; Chromium may have changed how it writes them.');
  return Buffer.from(fixed, 'latin1');
}

async function renderPdf(browser, html) {
  const page = await browser.newPage();
  await page.setContent(html, { waitUntil: 'load' });
  const pdf = await page.pdf({ preferCSSPageSize: true, printBackground: true });
  await page.close();
  return fixPdfDates(pdf);
}

async function renderJpeg(browser, html, { width, height, quality }) {
  const page = await browser.newPage({ viewport: { width, height }, deviceScaleFactor: 1 });
  await page.setContent(html, { waitUntil: 'load' });
  const jpeg = await page.screenshot({ type: 'jpeg', quality, clip: { x: 0, y: 0, width, height } });
  await page.close();
  return jpeg;
}

// ---------------------------------------------------------------------------------------------------------------
// EXIF
// ---------------------------------------------------------------------------------------------------------------

// A big-endian TIFF structure with one IFD, holding the tags Exif 2.3 requires in IFD0 of a JPEG file, plus
// Orientation, Software and DateTime. Tags stay in ascending order.
function exifApp1(orientation) {
  const SHORT = 3;
  const ascii = (tag, text) => ({ tag, type: 2, count: text.length + 1, bytes: Buffer.from(`${text}\0`, 'latin1') });
  const short = (tag, value) => ({ tag, type: SHORT, count: 1, value });
  const rational = (tag, numerator, denominator) => {
    const bytes = Buffer.alloc(8);
    bytes.writeUInt32BE(numerator, 0);
    bytes.writeUInt32BE(denominator, 4);
    return { tag, type: 5, count: 1, bytes };
  };
  const fields = [
    short(0x0112, orientation), // Orientation
    rational(0x011a, 72, 1), // XResolution
    rational(0x011b, 72, 1), // YResolution
    short(0x0128, 2), // ResolutionUnit: inches
    ascii(0x0131, 'ClaimLens golden generator (synthetic)'), // Software
    ascii(0x0132, '2026:10:12 09:30:00'), // DateTime
    short(0x0213, 1), // YCbCrPositioning: centred
  ];
  const ifd = Buffer.alloc(2 + fields.length * 12 + 4); // count, entries, next IFD offset 0
  const data = [];
  let dataOffset = 8 + ifd.length;
  ifd.writeUInt16BE(fields.length, 0);
  fields.forEach((field, i) => {
    const at = 2 + i * 12;
    ifd.writeUInt16BE(field.tag, at);
    ifd.writeUInt16BE(field.type, at + 2);
    ifd.writeUInt32BE(field.count, at + 4);
    if (field.type === SHORT) {
      ifd.writeUInt16BE(field.value, at + 8); // left-justified in the 4-byte value field
    } else {
      // Every ASCII and RATIONAL value here is longer than 4 bytes, so it goes after the IFD, at an even offset.
      ifd.writeUInt32BE(dataOffset, at + 8);
      const padded = field.bytes.length % 2 ? Buffer.concat([field.bytes, Buffer.alloc(1)]) : field.bytes;
      data.push(padded);
      dataOffset += padded.length;
    }
  });
  const tiff = Buffer.concat([Buffer.from([0x4d, 0x4d, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x08]), ifd, ...data]);
  const body = Buffer.concat([Buffer.from('Exif\0\0', 'latin1'), tiff]);
  const header = Buffer.alloc(4);
  header.writeUInt16BE(0xffe1, 0);
  header.writeUInt16BE(body.length + 2, 2);
  return Buffer.concat([header, body]);
}

// Puts the Exif APP1 segment right after SOI, as a camera does, in place of the JFIF APP0 Chromium writes.
function withOrientation(jpeg, orientation) {
  if (jpeg.readUInt16BE(0) !== 0xffd8) throw new Error('Not a JPEG.');
  let rest = 2;
  if (jpeg.readUInt16BE(rest) === 0xffe0) rest += 2 + jpeg.readUInt16BE(rest + 2);
  return Buffer.concat([jpeg.subarray(0, 2), exifApp1(orientation), jpeg.subarray(rest)]);
}

// ---------------------------------------------------------------------------------------------------------------
// ZIP, DOCX and XLSX
// ---------------------------------------------------------------------------------------------------------------

// 12/10/2026 09:00:00 in MS-DOS format, for every entry.
const DOS_DATE = ((2026 - 1980) << 9) | (10 << 5) | 12;
const DOS_TIME = (9 << 11) | (0 << 5) | 0;

function zip(entries) {
  const locals = [];
  const centrals = [];
  let offset = 0;
  for (const [name, content] of entries) {
    const nameBytes = Buffer.from(name, 'utf8');
    const data = Buffer.from(content, 'utf8');
    const crc = crc32(data);
    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(10, 4); // version needed: 1.0, stored
    local.writeUInt16LE(0, 6); // flags
    local.writeUInt16LE(0, 8); // method: stored
    local.writeUInt16LE(DOS_TIME, 10);
    local.writeUInt16LE(DOS_DATE, 12);
    local.writeUInt32LE(crc, 14);
    local.writeUInt32LE(data.length, 18);
    local.writeUInt32LE(data.length, 22);
    local.writeUInt16LE(nameBytes.length, 26);
    local.writeUInt16LE(0, 28); // no extra field
    const central = Buffer.alloc(46);
    central.writeUInt32LE(0x02014b50, 0);
    central.writeUInt16LE(20, 4); // made by: MS-DOS, 2.0
    central.writeUInt16LE(10, 6);
    central.writeUInt16LE(0, 8);
    central.writeUInt16LE(0, 10);
    central.writeUInt16LE(DOS_TIME, 12);
    central.writeUInt16LE(DOS_DATE, 14);
    central.writeUInt32LE(crc, 16);
    central.writeUInt32LE(data.length, 20);
    central.writeUInt32LE(data.length, 24);
    central.writeUInt16LE(nameBytes.length, 28);
    central.writeUInt32LE(offset, 42); // extra, comment, disk, attributes stay 0
    locals.push(local, nameBytes, data);
    centrals.push(central, nameBytes);
    offset += local.length + nameBytes.length + data.length;
  }
  const directory = Buffer.concat(centrals);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(entries.length, 8);
  end.writeUInt16LE(entries.length, 10);
  end.writeUInt32LE(directory.length, 12);
  end.writeUInt32LE(offset, 16);
  return Buffer.concat([...locals, directory, end]);
}

const XML = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n';
const RELS_NS = 'http://schemas.openxmlformats.org/package/2006/relationships';
const OFFICE_DOCUMENT = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument';

const contentTypes = (overrides) =>
  `${XML}<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">` +
  '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>' +
  '<Default Extension="xml" ContentType="application/xml"/>' +
  overrides.map(([part, type]) => `<Override PartName="${part}" ContentType="${type}"/>`).join('') +
  '</Types>';

const relationships = (rels) =>
  `${XML}<Relationships xmlns="${RELS_NS}">` +
  rels.map(([id, type, target]) => `<Relationship Id="${id}" Type="${type}" Target="${target}"/>`).join('') +
  '</Relationships>';

// DOCX: [Content_Types].xml, _rels/.rels and word/document.xml. Runs carry their own font, so no styles part is needed.
function run(text, { bold = false, italic = false, size = 22, color } = {}) {
  const props =
    '<w:rFonts w:ascii="Arial" w:hAnsi="Arial" w:cs="Arial"/>' +
    (bold ? '<w:b/>' : '') +
    (italic ? '<w:i/>' : '') +
    (color ? `<w:color w:val="${color}"/>` : '') +
    `<w:sz w:val="${size}"/><w:szCs w:val="${size}"/>`;
  return `<w:r><w:rPr>${props}</w:rPr><w:t xml:space="preserve">${escape(text)}</w:t></w:r>`;
}

function para(runs, { align, after = 80, before = 0 } = {}) {
  const props = `<w:spacing w:before="${before}" w:after="${after}"/>` + (align ? `<w:jc w:val="${align}"/>` : '');
  return `<w:p><w:pPr>${props}</w:pPr>${[runs].flat().join('')}</w:p>`;
}

function wordTable(widths, rows) {
  const border = (side) => `<w:${side} w:val="single" w:sz="6" w:space="0" w:color="555555"/>`;
  const borders = ['top', 'left', 'bottom', 'right', 'insideH', 'insideV'].map(border).join('');
  const cell = (content, width, header) =>
    `<w:tc><w:tcPr><w:tcW w:w="${width}" w:type="dxa"/>${header ? '<w:shd w:val="clear" w:color="auto" w:fill="EFE9DF"/>' : ''}</w:tcPr>` +
    `${para(run(content, { bold: header, size: 21 }), { after: 40, before: 40 })}</w:tc>`;
  return (
    `<w:tbl><w:tblPr><w:tblW w:w="${widths.reduce((a, b) => a + b, 0)}" w:type="dxa"/><w:tblBorders>${borders}</w:tblBorders></w:tblPr>` +
    `<w:tblGrid>${widths.map((w) => `<w:gridCol w:w="${w}"/>`).join('')}</w:tblGrid>` +
    rows.map((row, r) => `<w:tr>${row.map((text, c) => cell(text, widths[c], r === 0)).join('')}</w:tr>`).join('') +
    '</w:tbl>'
  );
}

function prescriptionDocx() {
  const { member, provider, diagnosis } = golden;
  const body = [
    para(run(provider.name.toUpperCase(), { bold: true, size: 26, color: '8B1D1D' }), { after: 0 }),
    para(run(`Địa chỉ: ${provider.address}`, { size: 20 }), { after: 0 }),
    para(run(`Mã số thuế: ${provider.taxCode} · Điện thoại: ${provider.phone}`, { size: 20 }), { after: 240 }),
    para(run('ĐƠN THUỐC', { bold: true, size: 36 }), { align: 'center', after: 40 }),
    para(run('Số đơn: ĐT-2026-018437', { italic: true, size: 20 }), { align: 'center', after: 240 }),
    para([run('Họ tên người bệnh: '), run(member.name, { bold: true })]),
    para(run(`Ngày sinh: ${member.birthDate}    Giới tính: ${member.gender}    Số CCCD: ${member.nationalId}`)),
    para(run(`Mã thành viên bảo hiểm: ${member.code}    Số hợp đồng: ${golden.policy}`)),
    para(run(`Địa chỉ: ${member.address}`)),
    para([run('Chẩn đoán: '), run(`${diagnosis.code} – ${diagnosis.name}`, { bold: true })], { after: 200 }),
    para(run('Thuốc điều trị:', { bold: true }), { after: 80 }),
    wordTable(
      [700, 2900, 1400, 4600],
      [
        ['STT', 'Tên thuốc, hàm lượng', 'Số lượng', 'Cách dùng'],
        ['1', 'Amoxicillin 500mg', '20 viên', 'Uống 1 viên/lần, ngày 2 lần (sáng, tối) sau ăn, trong 10 ngày'],
      ],
    ),
    para(run('Lời dặn: Uống đủ nước, súc họng nước muối ấm. Tái khám nếu sốt trên 38,5°C hoặc sau 10 ngày không đỡ.'), {
      before: 200,
      after: 360,
    }),
    para(run(longDate(golden.prescriptionDate), { italic: true }), { align: 'right', after: 0 }),
    para(run('Bác sĩ điều trị', { bold: true }), { align: 'right', after: 0 }),
    para(run('(Ký, ghi rõ họ tên)', { italic: true, size: 18 }), { align: 'right', after: 900 }),
    para(run(golden.doctor, { bold: true }), { align: 'right', after: 600 }),
    para(run(SYNTHETIC, { italic: true, size: 18, color: '666666' }), { align: 'center' }),
  ].join('');
  const document =
    `${XML}<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body>${body}` +
    '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/>' +
    '<w:pgMar w:top="1134" w:right="1134" w:bottom="1134" w:left="1134" w:header="0" w:footer="0" w:gutter="0"/>' +
    '</w:sectPr></w:body></w:document>';
  return zip([
    [
      '[Content_Types].xml',
      contentTypes([
        ['/word/document.xml', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'],
      ]),
    ],
    ['_rels/.rels', relationships([['rId1', OFFICE_DOCUMENT, 'word/document.xml']])],
    ['word/document.xml', document],
  ]);
}

// XLSX: content types, two relationship parts, workbook and one sheet. Strings are inline, so no shared strings part;
// amounts use the General format, so no styles part. The totals are formulas with their values cached.
function itemisedBillXlsx() {
  const { member, provider, invoice, diagnosis, lines } = golden;
  const text = (ref, value) => `<c r="${ref}" t="inlineStr"><is><t xml:space="preserve">${escape(value)}</t></is></c>`;
  const number = (ref, value) => `<c r="${ref}"><v>${value}</v></c>`;
  const formula = (ref, expression, value) => `<c r="${ref}"><f>${expression}</f><v>${value}</v></c>`;
  const rows = [];
  const row = (r, cells) => rows.push(`<row r="${r}">${cells.join('')}</row>`);
  row(1, [text('A1', provider.name.toUpperCase())]);
  row(2, [text('A2', `Mã số thuế: ${provider.taxCode} · ${provider.address}`)]);
  row(4, [text('A4', 'BẢNG KÊ CHI PHÍ KHÁM, CHỮA BỆNH NGOẠI TRÚ')]);
  row(5, [text('A5', `Họ tên người bệnh: ${member.name}`), text('D5', `Mã thành viên: ${member.code}`)]);
  row(6, [text('A6', `Ngày khám: ${slash(golden.serviceDate)}`), text('D6', `Số hợp đồng: ${golden.policy}`)]);
  row(7, [text('A7', `Chẩn đoán: ${diagnosis.code} – ${diagnosis.name}`), text('D7', `Hóa đơn: ${invoice.number}`)]);
  row(9, ['STT', 'Nội dung', 'Đơn vị tính', 'Số lượng', 'Đơn giá (VNĐ)', 'Thành tiền (VNĐ)'].map((h, i) => text(`${'ABCDEF'[i]}9`, h)));
  const first = 10;
  lines.forEach((line, i) => {
    const r = first + i;
    row(r, [
      number(`A${r}`, line.no),
      text(`B${r}`, line.text),
      text(`C${r}`, line.unit),
      number(`D${r}`, line.qty),
      number(`E${r}`, line.price),
      formula(`F${r}`, `D${r}*E${r}`, line.amount),
    ]);
  });
  const last = first + lines.length - 1;
  row(last + 1, [text(`B${last + 1}`, 'Tổng cộng'), formula(`F${last + 1}`, `SUM(F${first}:F${last})`, golden.total)]);
  row(last + 2, [text(`B${last + 2}`, `Số tiền viết bằng chữ: ${golden.totalInWords}.`)]);
  row(last + 4, [text(`D${last + 4}`, longDate(golden.serviceDate))]);
  row(last + 5, [text(`D${last + 5}`, `Người lập bảng: ${golden.cashier}`)]);
  row(last + 7, [text(`A${last + 7}`, SYNTHETIC)]);
  const widths = [6, 34, 12, 10, 16, 18];
  const sheet =
    `${XML}<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">` +
    '<sheetPr><pageSetUpPr fitToPage="1"/></sheetPr>' +
    `<dimension ref="A1:F${last + 7}"/>` +
    `<cols>${widths.map((w, i) => `<col min="${i + 1}" max="${i + 1}" width="${w}" customWidth="1"/>`).join('')}</cols>` +
    `<sheetData>${rows.join('')}</sheetData>` +
    '<printOptions gridLines="1"/>' +
    '<pageMargins left="0.6" right="0.6" top="0.8" bottom="0.8" header="0.3" footer="0.3"/>' +
    '<pageSetup paperSize="9" orientation="portrait" fitToWidth="1" fitToHeight="0"/>' +
    '</worksheet>';
  const workbook =
    `${XML}<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" ` +
    'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">' +
    '<sheets><sheet name="Bảng kê chi phí" sheetId="1" r:id="rId1"/></sheets></workbook>';
  const worksheetType = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet';
  return zip([
    [
      '[Content_Types].xml',
      contentTypes([
        ['/xl/workbook.xml', 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'],
        ['/xl/worksheets/sheet1.xml', 'application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml'],
      ]),
    ],
    ['_rels/.rels', relationships([['rId1', OFFICE_DOCUMENT, 'xl/workbook.xml']])],
    ['xl/workbook.xml', workbook],
    ['xl/_rels/workbook.xml.rels', relationships([['rId1', worksheetType, 'worksheets/sheet1.xml']])],
    ['xl/worksheets/sheet1.xml', sheet],
  ]);
}

// ---------------------------------------------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------------------------------------------

function write(relativePath, bytes) {
  const path = join(outRoot, relativePath);
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, bytes);
  console.log(`${relativePath.replaceAll('\\', '/').padEnd(42)} ${String(bytes.length).padStart(7)} bytes`);
}

const browser = await chromium.launch();
try {
  const receiptSize = { width: RECEIPT.height, height: RECEIPT.width, quality: 85 };
  write('clean/documents/invoice.pdf', await renderPdf(browser, invoiceHtml()));
  write(
    'clean/documents/receipt.jpg',
    withOrientation(await renderJpeg(browser, receiptHtml({ rotate: true }), receiptSize), 6),
  );
  write('clean/documents/prescription.docx', prescriptionDocx());
  write('clean/documents/itemised-bill.xlsx', itemisedBillXlsx());
  write('clean/documents/visit-summary.pdf', await renderPdf(browser, visitSummaryHtml()));
  write(
    'blurry/documents/receipt-blurry.jpg',
    await renderJpeg(browser, receiptHtml({ blur: 3 }), { ...RECEIPT, quality: 85 }),
  );
  write(
    'invalid/not-a-docx.docx',
    Buffer.from(
      'Đây là văn bản thuần, không phải tệp DOCX, dù tên tệp có đuôi .docx.\n' +
        'Claims API phải từ chối tệp này theo các byte đầu tiên, không theo tên.\n',
      'utf8',
    ),
  );
} finally {
  await browser.close();
}
