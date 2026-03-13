# Snippets Settings

The **Snippets** tab provides a robust, instantaneous text-expansion engine built natively into the dIKta.me text injection pipeline.

Because dictation formatting can sometimes be verbose, Snippets allow you to define highly customized shorthand commands that dIKta.me automatically expands into complex text blocks, templates, or repetitive data exactly the moment it is injected into your active application.

## How Snippets Work

A Snippet acts like a quick macro. When the Speech-to-Text and LLM pipelines are fully finished processing, dIKta.me executes a final sweep over the completed text looking for your exact Snippet Triggers. 

If it finds one, it immediately replaces the trigger word with the expanded snippet content and pastes the final text. 

> [!TIP]
> **Post-LLM Expansion**: Because Snippets are expanded *after* the LLM has finished formatting the text, it guarantees the LLM doesn't accidentally hallucinate, misformat, or re-word your strictly defined template characters! It is an absolute string replacement.

## Creating Snippets

Inside the Snippets settings tab, you can add, configure, and delete Snippets instantly.

Each Snippet requires two fields:
*   **Trigger Word**: The exact sequence of characters you want to detect (e.g. `//myemail`, `@@sig`, `/template1`).
*   **Replacement Content**: The exact, verbatim block of text you want it to expand into. Supports multi-line templates, specific legal disclaimers, or complicated URLs.

### Example Dictation
If you create a snippet natively where:
*   Trigger = `:meeting:`
*   Content = `Meeting Notes:\nAttendees: \nSummary: \nAction Items: `

If you then say out loud into the microphone:
> *"Hey team, let's look at the :meeting: structure."*

dIKta.me will seamlessly format it down to your cursor as:
> *Hey team, let's look at the Meeting Notes:
> Attendees: 
> Summary: 
> Action Items:  structure.*

## Best Practices
1.  **Unique Triggers**: Always prefix your Trigger Words with special punctuation (like `:` or `//` or `@@`) to ensure dIKta.me does not accidentally expand a normal word you happen to say in a sentence.
2.  **Toggle Snippets**: You can globally enable or disable the entire Snippet expansion engine securely at the top of the Settings page if you do not want to trigger templates by accident.
