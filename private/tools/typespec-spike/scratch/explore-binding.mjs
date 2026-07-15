// Explore the raw op.parameters vs getHttpOperation(...) shapes for `sign`,
// so the SC-1 dual-binding check can be written against the real graph.
import { NodeHost, compile, navigateProgram, getTypeName } from "@typespec/compiler";
import { getHttpOperation } from "@typespec/http";

const mainFile = new URL("../prototype.compile.tsp", import.meta.url).pathname.replace(
  /^\/([A-Za-z]:)/,
  "$1",
);
const program = await compile(NodeHost, mainFile, { noEmit: true });
if (program.diagnostics.length) {
  for (const d of program.diagnostics) console.log("DIAG:", d.code, d.message);
}

let signOp;
navigateProgram(program, {
  operation(op) {
    if (op.name === "sign") signOp = op;
  },
});

console.log("=== found op:", signOp && getTypeName(signOp));

// ---- RAW model graph: op.parameters ----
const params = signOp.parameters; // a Model whose properties are the op params
console.log("\n--- op.parameters (raw model) ---");
console.log("op.parameters kind:", params.kind, " name:", params.name || "(anonymous)");
console.log("op.parameters property keys:", [...params.properties.keys()]);
for (const [pname, prop] of params.properties) {
  const t = prop.type;
  console.log(`  param '${pname}': type.kind=${t.kind} type.name=${t.name ?? "(anon)"}`);
  if (t.kind === "Model") {
    console.log(`     -> Model '${t.name}' props:`, [...t.properties.keys()]);
    const payload = t.properties.get("payload");
    if (payload) console.log(`     -> payload.type.kind=${payload.type.kind} name=${payload.type.name ?? payload.type.kind}`);
  }
}

// ---- HTTP view: getHttpOperation ----
const [httpOp, diags] = getHttpOperation(program, signOp);
if (diags?.length) for (const d of diags) console.log("HTTP DIAG:", d.code, d.message);
console.log("\n--- getHttpOperation(...) ---");
console.log("verb:", httpOp.verb, " path:", httpOp.path);
const body = httpOp.parameters.body;
console.log("body present:", !!body);
if (body) {
  console.log("body.type kind:", body.type.kind, " name:", body.type.name ?? "(anon)");
  console.log("body.property (the @body param):", body.property?.name);
}

// ---- The IDENTITY test ----
const rawBodyModel = params.properties.get("input")?.type; // SignInput from raw graph
const httpBodyModel = body?.type; // SignInput from HTTP view
console.log("\n--- IDENTITY TEST ---");
console.log("raw body model name:", rawBodyModel?.name);
console.log("http body model name:", httpBodyModel?.name);
console.log("SAME OBJECT (===)?:", rawBodyModel === httpBodyModel);
console.log("raw payload field kind:", rawBodyModel?.properties?.get("payload")?.type?.kind);
console.log("http payload field kind:", httpBodyModel?.properties?.get("payload")?.type?.kind);
