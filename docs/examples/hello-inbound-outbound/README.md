# Hello inbound/outbound

Run the minimal validation flow used in the main README, but as a runnable FSI script.

Steps:

1. Build the library (from repo root): `dotnet build src/ACP.fsproj`
2. Execute the script from repo root (path matters for the `#I`): `dotnet fsi docs/examples/hello-inbound-outbound/hello.fsx`

What it does:

- Loads the compiled `ACP.dll` from `src/bin/Debug/net9.0/`
- Constructs one inbound initialize and one outbound initialize-result message
- Runs `validateInbound` and `validateOutbound`
- Prints findings and final phases so you can sanity check wiring

Tweak the messages (e.g., flip `writeTextFile` to true) to see validation findings change.
