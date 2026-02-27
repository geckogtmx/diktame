namespace DiktaMe.Core.Config;

/// <summary>
/// Factory for creating the 4 built-in dictation modes and 5 utility pipeline configs.
/// Called by SettingsManager when initializing AppSettings for the first time.
/// Port of V1's mode system (Standard, Prompt, Professional, RAW) with dual-profile architecture.
/// </summary>
public static class DictationModeDefaults
{
    /// <summary>
    /// Creates the 4 built-in dictation modes with default prompts and hotkeys.
    /// </summary>
    public static List<DictationMode> CreateBuiltInModes()
    {
        return
        [
            new DictationMode
            {
                Id = "dictate-standard",
                Title = "Standard",
                IsBuiltIn = true,
                SortOrder = 0,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.Dictate,
                    UseLlm = true,
                    ModelName = "gpt-4o-mini", // default Cloud model (fast + cheap)
                    Hotkey = "Ctrl+Alt+D",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.Dictate,
                    UseLlm = true,
                    ModelName = null, // Ollama model from AppSettings.OllamaModelName
                    Hotkey = "Ctrl+Alt+D",
                },
            },

            new DictationMode
            {
                Id = "dictate-prompt",
                Title = "Prompt",
                IsBuiltIn = true,
                SortOrder = 1,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = "Follow the user's custom instruction exactly. Return ONLY the result.",
                    UseLlm = true,
                    ModelName = "gpt-4o", // more capable for custom instructions
                    Hotkey = "Ctrl+Alt+P",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = "Follow the user's custom instruction exactly. Return ONLY the result.",
                    UseLlm = true,
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+P",
                },
            },

            new DictationMode
            {
                Id = "dictate-professional",
                Title = "Professional",
                IsBuiltIn = true,
                SortOrder = 2,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = "Transform this into formal, professional business writing. Fix grammar, remove filler words, use active voice. Return ONLY the polished text.",
                    UseLlm = true,
                    ModelName = "claude-sonnet-4", // Claude for style + tone
                    Hotkey = "Ctrl+Alt+Shift+D",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = "Transform this into formal, professional business writing. Fix grammar, remove filler words, use active voice. Return ONLY the polished text.",
                    UseLlm = true,
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+Shift+D",
                },
            },

            new DictationMode
            {
                Id = "dictate-raw",
                Title = "RAW",
                IsBuiltIn = true,
                SortOrder = 3,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = null,
                    UseLlm = false, // raw transcription, no LLM
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+R",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = null,
                    UseLlm = false,
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+R",
                },
            },
        ];
    }

    /// <summary>
    /// Creates the 5 built-in utility pipeline configs with default prompts and hotkeys.
    /// </summary>
    public static List<PipelineConfig> CreateBuiltInUtilityPipelines()
    {
        return
        [
            new PipelineConfig
            {
                PipelineType = "ask",
                Hotkey = "Ctrl+Alt+A",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Ask,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Ask,
                    ModelName = null, // Ollama global model
                },
            },

            // Refine Auto — no audio, captures selection and cleans it up
            new PipelineConfig
            {
                PipelineType = "refine_auto",
                Hotkey = null, // No hotkey yet (to be added in future)
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.RefineAuto,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.RefineAuto,
                    ModelName = null,
                },
            },

            // Refine Instruction — records audio, applies spoken instruction to selection
            new PipelineConfig
            {
                PipelineType = "refine_instruction",
                Hotkey = "Ctrl+Alt+F",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.RefineInstruction,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.RefineInstruction,
                    ModelName = null,
                },
            },

            // Legacy "refine" entry for backward compatibility with existing settings.json
            new PipelineConfig
            {
                PipelineType = "refine",
                Hotkey = "Ctrl+Alt+R",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.RefineInstruction,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.RefineInstruction,
                    ModelName = null,
                },
            },

            new PipelineConfig
            {
                PipelineType = "translate",
                Hotkey = "Ctrl+Alt+T",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Translate,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Translate,
                    ModelName = null,
                },
            },

            new PipelineConfig
            {
                PipelineType = "note",
                Hotkey = "Ctrl+Alt+N",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Note,
                    ModelName = "gpt-4o-mini",
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Note,
                    ModelName = null,
                },
            },

            new PipelineConfig
            {
                PipelineType = "chat",
                Hotkey = "Ctrl+Alt+C",
                CloudProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Chat,
                    ModelName = "gpt-4o", // more conversational
                },
                LocalProfile = new UtilityProfile
                {
                    SystemPrompt = PromptDefaults.Chat,
                    ModelName = null,
                },
            },
        ];
    }
}
