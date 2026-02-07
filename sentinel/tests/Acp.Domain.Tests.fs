module Acp.Tests.DomainTests

open System.Text.RegularExpressions
open Xunit

module ``Spec Schema`` =

    [<Fact>]
    let ``Schema is pinned and semver`` () =
        let schema = Acp.Domain.Spec.Schema
        Assert.False(schema.Contains('x') || schema.Contains('X'))
        Assert.True(Regex.IsMatch(schema, @"^\d+\.\d+\.\d+$"))
