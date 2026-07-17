import "@typespec/http";
using TypeSpec.Http;

@route("/x")
op foo(): void;
