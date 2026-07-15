import "@typespec/http";
import "@d2/typespec-decorators";
using TypeSpec.Http;
using D2;

model SignInput { keyDomain: string; @d2Redact payload: bytes; }
model SignOutput { signature: string; kid: string; }

@route("/internal/v1/kc")
interface KCOps {
  // Mixed: a path param AND a @body model. This is the case the EMITTERS.md
  // note worries about (HTTP flattening path params into the op signature).
  @post
  @route("/{tenant}/sign")
  @d2GrpcMethod("KeyCustodianInternal", "Sign")
  sign(@path tenant: string, @body input: SignInput): SignOutput;
}
