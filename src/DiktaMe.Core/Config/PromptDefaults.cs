namespace DiktaMe.Core.Config;

/// <summary>
/// Built-in default system prompts for each workflow mode.
/// Used when ModeSettings.PromptSlot = -1 (no custom prompt configured).
/// Port of V1's python/config/prompts.py.
/// </summary>
public static class PromptDefaults
{
    /// <summary>
    /// Default prompt for Dictation mode (standard cleanup).
    /// Fixes punctuation, capitalization, removes filler words while preserving slang.
    /// </summary>
    public const string Dictate = """
        You are a text cleanup tool. Fix punctuation and capitalization.
        Remove filler words (um, uh) only if hesitations. PRESERVE slang/emphasis.
        Return ONLY cleaned text. Do not include introductory text.
        """;

    /// <summary>
    /// Default prompt for Refine mode (grammar + clarity improvement).
    /// Legacy — kept for backward compatibility. New code should use RefineAuto or RefineInstruction.
    /// </summary>
    public const string Refine = """
        Fix grammar, improve clarity. Return only refined text.
        """;

    /// <summary>
    /// Refine Auto mode — captures selection, cleans it up with LLM, replaces in-place.
    /// No audio recording, no {instruction} placeholder.
    /// </summary>
    public const string RefineAuto = """
        Fix grammar, improve clarity, preserve meaning. Return only refined text.
        """;

    /// <summary>
    /// Refine Instruction mode — captures selection, applies spoken instruction to it.
    /// Uses {instruction} placeholder for the transcribed verbal command.
    /// </summary>
    public const string RefineInstruction = """
        Apply this instruction to the selected text: {instruction}
        Return only the result.
        """;

    /// <summary>
    /// Default prompt for Ask mode (Q&amp;A).
    /// Returns direct, concise answers without conversational fillers.
    /// </summary>
    public const string Ask = """
        Answer the user's question directly and concisely.
        Rules:
        1. Return ONLY the answer text.
        2. NO conversational fillers at all

        ANSWER:
        """;

    /// <summary>
    /// Default prompt for Translate mode (bidirectional EN&lt;-&gt;ES).
    /// Auto-detects source language and translates to the opposite.
    /// </summary>
    public const string Translate = """
        Translate between English and Spanish. Auto-detect source language.
        Return ONLY the translation, no explanations.
        """;

    /// <summary>
    /// Default prompt for Note mode (optional formatting).
    /// Formats voice notes as clear, grammatically correct entries.
    /// </summary>
    public const string Note = """
        Format this as a clear note entry. Fix grammar and structure but preserve meaning.
        Return only the formatted note.
        """;

    /// <summary>
    /// Default prompt for Chat mode (conversational assistant).
    /// Friendly but concise Q&amp;A for the Quick Chat overlay.
    /// </summary>
    public const string Chat = """
        You are a helpful assistant. Answer questions conversationally but concisely.
        Be friendly and clear.
        """;

    /// <summary>
    /// Returns the default prompt for a given mode.
    /// </summary>
    /// <param name="mode">The mode name (case-insensitive): dictate, refine, ask, translate, note, chat.</param>
    /// <returns>The built-in default prompt string for that mode.</returns>
    public static string GetDefault(string mode) => mode.ToLowerInvariant() switch
    {
        "dictate" => Dictate,
        "refine" => RefineInstruction,  // legacy fallback
        "refine_auto" => RefineAuto,
        "refine_instruction" => RefineInstruction,
        "ask" => Ask,
        "translate" => Translate,
        "note" => Note,
        "chat" => Chat,
        _ => Dictate, // fallback to dictate for unknown modes
    };
}
