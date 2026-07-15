// Compile a tiny tsp and walk the namespace tree to find where the @d2* decorators bind.
import { NodeHost, compile } from "@typespec/compiler";

const mainFile = new URL("./probe-decl-only.tsp", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");

const program = await compile(NodeHost, mainFile, { noEmit: true });

// Walk decorator declarations in global + child namespaces.
const global = program.getGlobalNamespaceType();

function dumpNs(ns, path) {
  const decNames = [...ns.decoratorDeclarations.keys()];
  const d2 = decNames.filter((n) => n.startsWith("d2"));
  if (d2.length) console.log(`namespace ${path || "(global)"} declares d2 decorators:`, d2.join(", "));
  for (const [name, child] of ns.namespaces) {
    dumpNs(child, path ? `${path}.${name}` : name);
  }
}
dumpNs(global, "");

console.log("\n--- diagnostics ---");
for (const d of program.diagnostics) {
  console.log(`${d.severity}: ${d.code}: ${d.message}`);
}
