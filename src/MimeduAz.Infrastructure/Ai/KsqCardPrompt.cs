namespace MimeduAz.Infrastructure.Ai;

/// <summary>Vision modelə göndərilən təlimat. Cavab yalnız JSON obyekt olmalıdır.</summary>
internal static class KsqCardPrompt
{
    public static string Build(int closedCount, int openCount)
    {
        var openRange = openCount > 0
            ? $"and {openCount} open-ended question(s) numbered {closedCount + 1}..{closedCount + openCount}"
            : "and no open-ended questions";

        return $$"""
            You are reading a photo of a student's answer sheet from an Azerbaijani school test
            (KSQ — kiçik summativ qiymətləndirmə).

            The sheet can be either:
            (a) a printed answer card with a grid: one row per question number and bubbles/cells A B C D E
                — the student fills, shades, crosses or ticks one of them; or
            (b) the test paper itself, where the student circled, underlined or ticked the chosen option
                letter (A–E) next to each question.

            The test has {{closedCount}} closed (multiple-choice) question(s) numbered 1..{{closedCount}}
            {{openRange}}.

            Extract:
            - "ad": the student's full name as written on the sheet. Keep Azerbaijani letters
              (ə, ı, ö, ü, ğ, ş, ç, İ). null if not written.
            - "variant": "A" or "B" if the test variant is marked or written on the sheet, otherwise null.
            - "kod": the student's code / ID number if present, otherwise null.
            - "cavablar": an array of exactly {{closedCount}} items, in question order. Each item is
              "A", "B", "C", "D" or "E", or null when the question is left blank or the mark cannot be read.
              If more than one option is marked, or a mark is erased/crossed out and the final choice is
              not clear, use null and add the question number to "unsure".
            - "aciq": an array of exactly {{openCount}} items — the answer the student wrote for each
              open-ended question, as short plain text, or null if empty/unreadable.
            - "unsure": an array of question numbers (integers) whose reading you are not confident about.
            - "qeyd": a short note in Azerbaijani about problems with the photo (blurry, cut off, dark,
              not an answer sheet), otherwise null.

            Do not guess: a wrong letter is worse than null. Ignore the question text and any teacher marks.

            Return ONLY a JSON object, no markdown, no explanation:
            {"ad": string|null, "variant": "A"|"B"|null, "kod": string|null, "cavablar": [...], "aciq": [...], "unsure": [...], "qeyd": string|null}
            """;
    }
}
