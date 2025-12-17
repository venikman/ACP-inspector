module Acp.Cli.Common.Security

open System
open System.IO

/// Maximum allowed file size for CLI input (100MB)
[<Literal>]
let MaxInputFileSizeBytes = 100L * 1024L * 1024L

/// Validate that a file path is safe to read from.
/// Prevents directory traversal attacks and ensures the path is normalized.
let validateInputPath (path: string) : Result<string, string> =
    if String.IsNullOrWhiteSpace(path) then
        Error "Path cannot be empty"
    else
        try
            // Get current working directory as base
            let baseDir = Directory.GetCurrentDirectory()
            let baseDirNormalized = Path.GetFullPath(baseDir)

            // Get full normalized path
            let fullPath = Path.GetFullPath(path)

            // Ensure the resolved path is within or equal to the base directory
            // This prevents directory traversal even after normalization
            if not (fullPath.StartsWith(baseDirNormalized, StringComparison.OrdinalIgnoreCase)) then
                Error $"Path '{path}' resolves outside the current directory and is not allowed"
            elif not (File.Exists(fullPath)) then
                Error $"File not found: {fullPath}"
            else
                // Check file size
                let fileInfo = FileInfo(fullPath)

                if fileInfo.Length > MaxInputFileSizeBytes then
                    let sizeMB = float fileInfo.Length / (1024.0 * 1024.0)

                    Error
                        $"File size ({sizeMB:F1} MB) exceeds maximum allowed size ({MaxInputFileSizeBytes / 1024L / 1024L} MB)"
                else
                    Ok fullPath
        with ex ->
            Error $"Invalid path: {ex.Message}"

/// Validate that an output path is safe to write to.
/// Prevents directory traversal and ensures parent directory exists.
let validateOutputPath (path: string) : Result<string, string> =
    if String.IsNullOrWhiteSpace(path) then
        Error "Path cannot be empty"
    else
        try
            // Get current working directory as base
            let baseDir = Directory.GetCurrentDirectory()
            let baseDirNormalized = Path.GetFullPath(baseDir)

            // Get full normalized path
            let fullPath = Path.GetFullPath(path)

            // Ensure the resolved path is within or equal to the base directory
            if not (fullPath.StartsWith(baseDirNormalized, StringComparison.OrdinalIgnoreCase)) then
                Error $"Path '{path}' resolves outside the current directory and is not allowed"
            else
                // Ensure parent directory exists
                let dir = Path.GetDirectoryName(fullPath)

                if String.IsNullOrEmpty(dir) then
                    Error "Cannot determine parent directory"
                elif not (Directory.Exists(dir)) then
                    Error $"Parent directory does not exist: {dir}"
                else
                    Ok fullPath
        with ex ->
            Error $"Invalid path: {ex.Message}"
