import { NodeHost, compile, navigateProgram } from "@typespec/compiler";
import { getHttpOperation } from "@typespec/http";
const mainFile = new URL("../prototype.compile.tsp", import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, "$1");
const program = await compile(NodeHost, mainFile, { noEmit: true });
let op; navigateProgram(program, { operation(o){ if(o.name==="sign") op=o; }});
const rawBody = op.parameters.properties.get("input")?.type;
const [http] = getHttpOperation(program, op);
const httpBody = http.parameters.body?.type;
// return-type model (SignOutput) — a DIFFERENT node
const ret = op.returnType; // union SignOutput | D2ErrorResponse
console.log("rawBody === httpBody (expect true):", rawBody === httpBody);
console.log("rawBody === op.returnType (expect false):", rawBody === ret);
console.log("rawBody === {} (expect false):", rawBody === {});
