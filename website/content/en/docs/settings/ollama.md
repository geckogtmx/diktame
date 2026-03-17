# Ollama Settings

The **Ollama** tab manages your Local, on-device offline AI models that power dIKta.me's "Local" environment route. It ensures absolute privacy because no data is sent over the internet for processing. 

Ollama is a powerful, lightweight framework running in the background of your PC. dIKta.me communicates heavily with it to format and parse your transcriptions completely privately.

## Server Configuration

By default, dIKta.me assumes Ollama is running on the standard Localhost environment on your PC.

*   **Host URL (`http://localhost:11434`)**: If you have a dedicated server tower running Ollama elsewhere on your network, change this URL to point the LLM processor at the right IP address. Otherwise, leave it as default.
*   **Test Connection**: Instantly verifies that your PC can actively speak with the Ollama server and lists the active installed background framework version.

## Model Management Hub

Instead of forcing you to use terminal commands, the dIKta.me Ollama tab acts as a comprehensive manager that integrates directly into the pipeline:

*   **Installed Models**: A live table of every model currently downloaded in your Ollama library, displaying file sizes and parameter structures (e.g. `llama3:8b`, `mistral`).
*   **Delete**: Instantly free up hard drive space by deleting an installed model natively inside the GUI.
*   **Download New Model**: Need `phi3` or `gemma`? Type the model tag into the pull bar and dIKta.me will show you a progress overlay as the model streams and installs directly into your framework. 

Once the models are installed here, you can seamlessly navigate back to the `AI Engine` tab and set any of these newly downloaded offline models as your primary Dictation formatter.
