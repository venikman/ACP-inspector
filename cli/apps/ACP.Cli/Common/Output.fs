module Acp.Cli.Common.Output

open System

/// ANSI color codes for terminal output
module Colors =
    let reset = "\u001b[0m"
    let red = "\u001b[31m"
    let green = "\u001b[32m"
    let yellow = "\u001b[33m"
    let blue = "\u001b[34m"
    let magenta = "\u001b[35m"
    let cyan = "\u001b[36m"
    let gray = "\u001b[90m"
    let bold = "\u001b[1m"

/// Check if terminal supports colors.
///
/// We intentionally disable ANSI sequences when either stdout or stderr is redirected so that:
/// - CI logs stay parseable
/// - users can redirect output to files without escape sequences
let supportsColor () =
    not (Console.IsOutputRedirected || Console.IsErrorRedirected)
    && Environment.GetEnvironmentVariable("NO_COLOR") = null
    && (Environment.GetEnvironmentVariable("TERM") <> null
        || Environment.GetEnvironmentVariable("COLORTERM") <> null)

/// Print colored output to console
let printColored (color: string) (message: string) =
    if supportsColor () then
        Console.Write(color)
        Console.Write(message)
        Console.Write(Colors.reset)
    else
        Console.Write(message)

let printColoredLine (color: string) (message: string) =
    printColored color message
    Console.WriteLine()

/// Print success message
let printSuccess (message: string) =
    printColoredLine Colors.green $"✓ {message}"

/// Print error message
let printError (message: string) =
    printColoredLine Colors.red $"✗ {message}"

/// Print warning message
let printWarning (message: string) =
    printColoredLine Colors.yellow $"⚠ {message}"

/// Print info message
let printInfo (message: string) =
    printColoredLine Colors.cyan $"ℹ {message}"

/// Print heading
let printHeading (message: string) =
    Console.WriteLine()
    printColoredLine Colors.bold message
    printColoredLine Colors.gray (String.replicate message.Length "─")

/// Print key-value pair
let printKeyValue (key: string) (value: string) =
    printColored Colors.gray $"{key}: "
    Console.WriteLine(value)
