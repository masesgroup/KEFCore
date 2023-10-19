# KEFCore: Template Usage Guide

For more information related to .NET templates look at https://docs.microsoft.com/en-us/dotnet/core/tools/dotnet-new-sdk-templates.

## Installation

To install the templates executes the following command within a command shell:

>
> dotnet new --install MASES.EntityFrameworkCore.KNet.Templates
>

The command installs the latest version and on success will list all templates added to the list of available templates.
There is single template:
1. kefcoreApp: a project to create a console application using Entity Framework Core provider for Apache Kafka

## Simple usage

To use one of the available templates run the following command:

>
> dotnet new kefcoreApp
>

the previous command will create a .NET project for an executable. The user shall modify the code to set-up, at least the Apache Kafka broker address, and then execute it against an Apache Kafka server.
The template create the topics and fill them, then execute queries on previously data loaded.