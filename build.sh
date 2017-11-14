#!/usr/bin/env bash

dotnet --info
dotnet restore --verbosity m
dotnet build
dotnet test test/Pomelo.Extensions.Caching.MySql.Tests