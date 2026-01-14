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

/// Print colored output to console
let printColored (color: string) (message: string) =
    Console.Write(color)
    Console.Write(message)
    Console.Write(Colors.reset)

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

/// Check if terminal supports colors
let supportsColor () =
    not (Console.IsOutputRedirected || Console.IsErrorRedirected)
    && Environment.GetEnvironmentVariable("NO_COLOR") = null
    && (Environment.GetEnvironmentVariable("TERM") <> null
        || Environment.GetEnvironmentVariable("COLORTERM") <> null)
