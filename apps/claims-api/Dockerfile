# The build context is the repository root, because the SQL in db/ is embedded in the binary.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY db ./db
COPY apps/claims-api/global.json apps/claims-api/Directory.Build.props apps/claims-api/Directory.Packages.props ./apps/claims-api/
COPY apps/claims-api/src ./apps/claims-api/src
# --os linux keeps only the Linux native libraries SkiaSharp and PDFium ship (libSkiaSharp.so, libpdfium.so), for the
# architecture this build runs on. SkiaSharp's NoDependencies variant needs no fontconfig in the runtime image.
RUN dotnet publish apps/claims-api/src/ClaimLens.Api -c Release -o /out --no-self-contained --os linux

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_URLS=http://+:8080
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClaimLens.Api.dll"]
