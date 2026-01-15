module Acp.Cli.Common.Security

open System
open System.IO

/// Maximum allowed file size for CLI input (100MB)
[<Literal>]
let MaxInputFileSizeBytes = 100L * 1024L * 1024L

/// Validate that a file path is safe to read from.
/// Prevents directory traversal attacks via relative paths while allowing absolute paths.
/// For a CLI tool, we trust the user's filesystem permissions - validation prevents
/// accidental traversal but doesn't restrict legitimate absolute path access.
let validateInputPath (path: string) : Result<string, string> =
    if String.IsNullOrWhiteSpace(path) then
        Error "Path cannot be empty"
    else
        try
            // Prevent directory traversal in relative paths
            // Check for ".." before normalization to catch traversal attempts
            if path.Contains("..") then
                Error $"Path contains directory traversal sequence '..' which is not allowed: {path}"
            else
                // Get full normalized path (handles ~, ./, and converts relative to absolute)
                let fullPath = Path.GetFullPath(path)

                if not (File.Exists(fullPath)) then
                    Error $"File not found: {fullPath}"
                else
                    // Check file size to prevent DoS from extremely large files
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
/// Prevents directory traversal via relative paths while allowing absolute paths.
/// Ensures parent directory exists.
let validateOutputPath (path: string) : Result<string, string> =
    if String.IsNullOrWhiteSpace(path) then
        Error "Path cannot be empty"
    else
        try
            // Prevent directory traversal in relative paths
            if path.Contains("..") then
                Error $"Path contains directory traversal sequence '..' which is not allowed: {path}"
            else
                // Get full normalized path
                let fullPath = Path.GetFullPath(path)

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
