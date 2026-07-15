const m = await import("@dcsv-io/d2-typespec-decorators");
console.log("exports:", Object.keys(m).sort().join(", "));
console.log("D2_GRPC_METHOD_KEY === Symbol.for('D2.d2GrpcMethod'):", m.D2_GRPC_METHOD_KEY === Symbol.for("D2.d2GrpcMethod"));
console.log("$decorators namespaces:", Object.keys(m.$decorators));
console.log("$decorators.D2 keys:", Object.keys(m.$decorators.D2));
