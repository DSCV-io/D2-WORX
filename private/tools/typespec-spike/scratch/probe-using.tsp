import "@d2/typespec-decorators";
using TypeSpec.Reflection;
using D2;

@d2ServedBy("KeyCustodian")
op foo(): void;
