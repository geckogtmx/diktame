namespace DiktaMe.Core.Config;

/// <summary>
/// Factory for creating the default dictation preset and utility pipeline configs.
/// Called by SettingsManager when initializing AppSettings for the first time.
/// </summary>
public static class DictationModeDefaults
{
    /// <summary>
    /// Creates the default dictation presets for first-run: Standard, Prompt, Professional.
    /// Ported from V1's python/config/prompts.py preset system.
    /// </summary>
    public static List<DictationMode> CreateDefaultPresets()
    {
        return
        [
            new DictationMode
            {
                Id = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                Title = "Standard",
                SortOrder = 0,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.Dictate,
                    UseLlm = true,
                    ModelName = "gemini-2.5-flash",
                    Hotkey = "Ctrl+Alt+D",
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.Dictate,
                    UseLlm = true,
                    ModelName = null,
                    Hotkey = "Ctrl+Alt+D",
                },
            },
            new DictationMode
            {
                Id = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                Title = "Prompt",
                SortOrder = 1,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.DictatePrompt,
                    UseLlm = true,
                    ModelName = "gemini-2.5-flash",
                    Hotkey = null,
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.DictatePrompt,
                    UseLlm = true,
                    ModelName = null,
                    Hotkey = null,
                },
            },
            new DictationMode
            {
                Id = Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture),
                Title = "Professional",
                SortOrder = 2,
                CloudProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.DictateProfessional,
                    UseLlm = true,
                    ModelName = "gemini-2.5-flash",
                    Hotkey = null,
                },
                LocalProfile = new DictationProfile
                {
                    SystemPrompt = PromptDefaults.DictateProfessional,
                    UseLlm = true,
                    ModelName = null,
                    Hotkey = null,
                },
            },
        ];
    }

    /// <summary>
    /// Creates the utility pipeline configs with default prompts and hotkeys.
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
                    ModelName = "gemini-2.5-flash",
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
                    ModelName = "gemini-2.5-flash",
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
                    ModelName = "gemini-2.5-flash",
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
                    ModelName = "gemini-2.5-flash",
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
                    ModelName = "gemini-2.5-flash",
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
                    ModelName = "gemini-2.5-flash",
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
                    ModelName = "gemini-2.5-flash",
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
