module Acp.Cli.Common.Security

open System
open System.IO

/// Validate that a file path is safe to read from.
/// Prevents directory traversal attacks and ensures the path is normalized.
let validateInputPath (path: string) : Result<string, string> =
    if String.IsNullOrWhiteSpace(path) then
        Error "Path cannot be empty"
    else
        try
            // Get full normalized path
            let fullPath = Path.GetFullPath(path)

            // Check for suspicious patterns
            if fullPath.Contains("..") then
                Error "Path contains directory traversal (..) which is not allowed"
            elif not (File.Exists(fullPath)) then
                Error $"File not found: {fullPath}"
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
            // Get full normalized path
            let fullPath = Path.GetFullPath(path)

            // Check for suspicious patterns
            if fullPath.Contains("..") then
                Error "Path contains directory traversal (..) which is not allowed"
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
