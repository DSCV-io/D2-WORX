import "@typespec/http";
import "@dcsv-io/d2-typespec-decorators";
using TypeSpec.Http;

model SignInput { keyDomain: string; @D2.d2Redact payload: bytes; }
model SignOutput { signature: string; kid: string; }

@route("/internal/v1/kc")
interface KeyCustodianInternalOps {
  @post
  @route("/sign")
  @D2.d2Scope("internal.kc.sign")
  @D2.d2GrpcMethod("KeyCustodianInternal", "Sign")
  @D2.d2ServedBy("KeyCustodian")
  sign(@body input: SignInput): SignOutput;
}
