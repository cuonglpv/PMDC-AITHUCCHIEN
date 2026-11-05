-- Vietnamese name search (US02). unaccent is not immutable, so the index and every search use a wrapper.

create extension if not exists pg_trgm;
create extension if not exists unaccent;

create function immutable_unaccent(text) returns text
  language sql immutable parallel safe
  as $$ select public.unaccent('public.unaccent', $1) $$;

create index ix_member_name_trgm
  on member using gin (immutable_unaccent(full_name) gin_trgm_ops);
