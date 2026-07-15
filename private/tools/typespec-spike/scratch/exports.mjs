const c = await import("@typespec/compiler");
console.log("=== compiler: state/navigate/emit/diagnostic/compile/host ===");
console.log(Object.keys(c).filter(k=>/state|navigate|emit|Diagnostic|compile|Program|NodeHost|resolve|getType|createType|format/i.test(k)).sort().join("\n"));
const h = await import("@typespec/http");
console.log("\n=== http: getHttp / route / operation ===");
console.log(Object.keys(h).filter(k=>/getHttp|getRoute|getOperation|listHttp|HttpOperation/i.test(k)).sort().join("\n"));
