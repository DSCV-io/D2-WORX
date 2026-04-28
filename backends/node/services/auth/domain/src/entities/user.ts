import { cleanDisplayStr, cleanAndValidateEmail, generateUuidV7 } from "@d2/utilities";
import { AuthValidationError } from "../exceptions/auth-validation-error.js";

export interface User {
  readonly id: string;
  readonly email: string;
  readonly name: string;
  readonly username: string;
  readonly displayUsername: string;
  readonly emailVerified: boolean;
  readonly image?: string;
  readonly locale: string;
  readonly timezone: string;
  readonly createdAt: Date;
  readonly updatedAt: Date;
}

export interface CreateUserInput {
  readonly email: string;
  readonly name: string;
  readonly username: string;
  readonly displayUsername: string;
  readonly id?: string;
  readonly image?: string;
  readonly emailVerified?: boolean;
  readonly locale?: string;
  readonly timezone?: string;
}

export interface UpdateUserInput {
  readonly name?: string;
  readonly email?: string;
  readonly username?: string;
  readonly displayUsername?: string;
  readonly emailVerified?: boolean;
  readonly image?: string;
  readonly locale?: string;
  readonly timezone?: string;
}

export function createUser(input: CreateUserInput): User {
  const email = cleanAndValidateEmail(input.email);

  const name = cleanDisplayStr(input.name);
  if (!name) {
    throw new AuthValidationError("User", "name", input.name, "is required.");
  }

  if (!input.username) {
    throw new AuthValidationError("User", "username", input.username, "is required.");
  }
  if (!input.displayUsername) {
    throw new AuthValidationError("User", "displayUsername", input.displayUsername, "is required.");
  }

  return {
    id: input.id ?? generateUuidV7(),
    email,
    name,
    username: input.username,
    displayUsername: input.displayUsername,
    emailVerified: input.emailVerified ?? false,
    image: input.image,
    locale: input.locale ?? "en-US",
    timezone: input.timezone ?? "America/New_York",
    createdAt: new Date(),
    updatedAt: new Date(),
  };
}

export function updateUser(user: User, updates: UpdateUserInput): User {
  let email = user.email;
  if (updates.email !== undefined) {
    email = cleanAndValidateEmail(updates.email);
  }

  let name = user.name;
  if (updates.name !== undefined) {
    const cleaned = cleanDisplayStr(updates.name);
    if (!cleaned) {
      throw new AuthValidationError("User", "name", updates.name, "is required.");
    }
    name = cleaned;
  }

  return {
    ...user,
    email,
    name,
    username: updates.username ?? user.username,
    displayUsername: updates.displayUsername ?? user.displayUsername,
    emailVerified: updates.emailVerified ?? user.emailVerified,
    image: updates.image !== undefined ? updates.image : user.image,
    locale: updates.locale ?? user.locale,
    timezone: updates.timezone ?? user.timezone,
    updatedAt: new Date(),
  };
}
