/**
 * ASCII art faces for each pet mood.
 */

export function happy(): string {
  return "(^‿^)";
}

export function sad(): string {
  return "(╥_╥)";
}

export function hungry(): string {
  return "(>_<)~";
}

export function sleepy(): string {
  return "(o_o)zzZ";
}

export function excited(): string {
  return "\\(★ω★)/";
}

/**
 * Returns the ASCII art face for the given mood string.
 */
export function forMood(mood: string): string {
  switch (mood.toLowerCase()) {
    case "happy":
      return happy();
    case "sad":
      return sad();
    case "hungry":
      return hungry();
    case "sleepy":
      return sleepy();
    case "excited":
      return excited();
    default:
      return happy();
  }
}
