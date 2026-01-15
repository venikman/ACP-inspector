namespace Acp.Tests

open System
open System.IO
open System.Text.RegularExpressions
open Xunit

module RepoPolicyTests =

    let private repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

    let private excludedParts =
        [ "/bin/"
          "/obj/"
          "/.git/"
          "/benchmarks/"
          "/tests/bin/"
          "/tests/obj/"
          "/tests/TestResults/" ]

    let private isExcludedPath (path: string) =
        let norm = path.Replace('\\', '/')
        excludedParts |> List.exists norm.Contains

    let private pythonConfigFileNames =
        set
            [ "requirements.txt"
              "pyproject.toml"
              "pipfile"
              "pipfile.lock"
              "poetry.lock"
              "setup.py"
              "setup.cfg"
              "tox.ini"
              ".python-version"
              "mypy.ini" ]

    let private pythonExtensions = set [ ".py"; ".pyi"; ".pyw"; ".ipynb" ]

    let private pythonCommandRegex =
        Regex(@"(^|\s)(python3?|pypy)(\s|$)", RegexOptions.IgnoreCase ||| RegexOptions.Compiled)

    let private hasPythonShebang (firstLine: string) =
        let trimmed = firstLine.TrimStart()

        trimmed.StartsWith("#!")
        && trimmed.Contains("python", StringComparison.OrdinalIgnoreCase)

    let private containsPythonCommand (line: string) =
        // Ignore commented lines (except shebang which we handle separately)
        let trimmed = line.TrimStart()

        if trimmed.StartsWith("#") then
            false
        else
            pythonCommandRegex.IsMatch(trimmed)

    [<Fact>]
    let ``repo contains no Python sources, configs, or script invocations`` () =
        let files =
            Directory.EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories)
            |> Seq.filter (fun p -> not (isExcludedPath p))
            |> Seq.toList

        let pythonFiles =
            files
            |> List.filter (fun path ->
                let ext = Path.GetExtension(path).ToLowerInvariant()
                let name = Path.GetFileName(path).ToLowerInvariant()
                pythonExtensions.Contains(ext) || pythonConfigFileNames.Contains(name))

        let pythonScriptInvocations =
            files
            |> List.choose (fun path ->
                let ext = Path.GetExtension(path).ToLowerInvariant()
                let name = Path.GetFileName(path)

                let shouldScan =
                    ext = ".sh" || ext = ".bash" || ext = ".zsh" || String.IsNullOrWhiteSpace ext

                if not shouldScan then
                    None
                else
                    try
                        let lines = File.ReadAllLines path

                        if lines.Length = 0 then
                            None
                        else if hasPythonShebang lines.[0] then
                            Some(sprintf "%s (python shebang)" path)
                        else if lines |> Array.exists containsPythonCommand then
                            Some(sprintf "%s (python invocation)" path)
                        else
                            None
                    with _ ->
                        None)

        let offenders =
            (pythonFiles |> List.map (fun p -> p + " (python file/config)"))
            @ pythonScriptInvocations

        Assert.True(
            offenders.IsEmpty,
            "Python is not allowed in this repository.\nOffenders:\n"
            + String.Join("\n", offenders)
        )
