import { jwtVerify, errors } from "jose";
import type { JWTPayload, JWTVerifyGetKey, JWTVerifyResult } from "jose";
import { D2Result } from "@d2/result";

export interface VerifyTokenOptions {
  readonly jwks: JWTVerifyGetKey;
  readonly issuer: string;
  readonly audience: string;
}

export interface VerifiedToken {
  readonly payload: JWTPayload;
  readonly protectedHeader: JWTVerifyResult["protectedHeader"];
}

/**
 * Verifies a JWT using RS256 against a JWKS provider.
 *
 * @returns `ok(VerifiedToken)` on success, `unauthorized()` on any failure.
 */
export async function verifyToken(
  token: string,
  options: VerifyTokenOptions,
): Promise<D2Result<VerifiedToken>> {
  try {
    const result = await jwtVerify(token, options.jwks, {
      issuer: options.issuer,
      audience: options.audience,
      algorithms: ["RS256"],
    });

    return D2Result.ok<VerifiedToken>({
      data: {
        payload: result.payload,
        protectedHeader: result.protectedHeader,
      },
    });
  } catch (error) {
    if (error instanceof errors.JWTExpired) {
      return D2Result.unauthorized({ messages: ["Token expired."] });
    }
    if (error instanceof errors.JWTClaimValidationFailed) {
      return D2Result.unauthorized({
        messages: [`Token claim validation failed: ${error.message}`],
      });
    }
    if (error instanceof errors.JWSSignatureVerificationFailed) {
      return D2Result.unauthorized({ messages: ["Token signature verification failed."] });
    }
    if (error instanceof errors.JWKSNoMatchingKey) {
      return D2Result.unauthorized({ messages: ["No matching key found in JWKS."] });
    }
    return D2Result.unauthorized({ messages: ["Token verification failed."] });
  }
}
