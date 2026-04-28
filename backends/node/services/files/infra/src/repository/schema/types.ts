import type { InferSelectModel, InferInsertModel } from "drizzle-orm";
import type { file } from "./tables.js";

export type FileRow = InferSelectModel<typeof file>;
export type NewFile = InferInsertModel<typeof file>;
