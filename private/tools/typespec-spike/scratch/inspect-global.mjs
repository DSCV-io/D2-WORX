import { NodeHost, compile } from "@typespec/compiler";
const mainFile = new URL("./probe-decl-only.tsp", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");
const program = await compile(NodeHost, mainFile, { noEmit: true });
const global = program.getGlobalNamespaceType();
console.log("GLOBAL decoratorDeclarations (d2*):", [...global.decoratorDeclarations.keys()].filter(n=>n.startsWith("d2")));
console.log("GLOBAL namespaces:", [...global.namespaces.keys()]);
const d2 = global.namespaces.get("D2");
console.log("D2 namespace exists:", !!d2);
if (d2) console.log("D2 decoratorDeclarations:", [...d2.decoratorDeclarations.keys()]);
