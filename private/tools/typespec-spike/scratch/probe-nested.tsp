import "@d2/typespec-decorators";
using D2.Contracts;

@d2ServedBy("KeyCustodian")
op foo(): void;
