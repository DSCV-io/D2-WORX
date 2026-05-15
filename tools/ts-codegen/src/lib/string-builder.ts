// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Indented string builder. Mirrors the Roslyn `StringBuilder` ergonomic
 * shape used in `.NET` source-generator emitters — same `appendLine` /
 * `increaseIndent` / `decreaseIndent` / `toString` operations so author
 * code reads identically across .NET and TS emitters.
 */
export class StringBuilder {
  private readonly buf: string[] = [];
  private indentLevel = 0;
  private readonly indent: string;

  constructor(indentSize = 2) {
    this.indent = " ".repeat(indentSize);
  }

  /** Append a single line at the current indent level. */
  appendLine(line = ""): this {
    if (line.length === 0) {
      this.buf.push("");
    } else {
      this.buf.push(this.indent.repeat(this.indentLevel) + line);
    }
    return this;
  }

  /** Increase indent for subsequent appendLine calls. */
  increaseIndent(): this {
    this.indentLevel++;
    return this;
  }

  /** Decrease indent for subsequent appendLine calls. */
  decreaseIndent(): this {
    if (this.indentLevel === 0)
      throw new RangeError("StringBuilder: indent already at zero");
    this.indentLevel--;
    return this;
  }

  toString(): string {
    return this.buf.join("\n");
  }
}
