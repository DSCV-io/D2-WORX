import { readFileSync, writeFileSync } from "node:fs";

export function mapIdentity(s) {
  let t = s;

  t = t.replaceAll(
    "@d2/key-custodian-client",
    "@dcsv-io/d2-private-key-custodian-client",
  );
  t = t.replaceAll("@d2/", "@dcsv-io/d2-");

  t = t.replace(
    /(?<![\w.])D2\.Shared\.Auth\.Abstractions\.Extensions/g,
    "DcsvIo.D2.Private.Auth.Abstractions.Extensions",
  );
  t = t.replace(
    /(?<![\w.])D2\.Shared\.Encryption\.Extensions/g,
    "DcsvIo.D2.Private.Encryption.Extensions",
  );
  t = t.replace(
    /(?<![\w.])D2\.Shared\.I18n\.Keys\.Extensions/g,
    "DcsvIo.D2.Private.I18n.Keys.Extensions",
  );

  t = t.replace(
    /(?<![\w.])D2\.Edge\.KeyCustodian/g,
    "DcsvIo.D2.Private.Edge.KeyCustodian",
  );
  t = t.replace(/(?<![\w.])D2\.Edge\./g, "DcsvIo.D2.Private.Edge.");
  t = t.replace(/(?<![\w.])D2\.Edge(?![\w.])/g, "DcsvIo.D2.Private.Edge");
  t = t.replace(/(?<![\w.])D2\.Audit\./g, "DcsvIo.D2.Private.Audit.");
  t = t.replace(/(?<![\w.])D2\.Audit(?![\w.])/g, "DcsvIo.D2.Private.Audit");

  t = t.replace(/(?<![\w.])D2\.Shared\./g, "DcsvIo.D2.");
  t = t.replace(/(?<![\w.])D2\.Shared(?![\w.])/g, "DcsvIo.D2");

  t = t.replace(/(?<![\w.])D2\.Private\./g, "DcsvIo.D2.Private.");
  t = t.replace(/(?<![\w.])D2\.Private(?![\w.])/g, "DcsvIo.D2.Private");

  t = t.replace(
    /(?<!global::)(?<![\w.])D2\.Services\./g,
    "global::D2.Services.",
  );

  return t;
}

const files = process.argv.slice(2);
for (const f of files) {
  const o = readFileSync(f, "utf8");
  const n = mapIdentity(o);
  writeFileSync(f, n);
  console.log(f, o === n ? "unchanged" : "mapped");
}
