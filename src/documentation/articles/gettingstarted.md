# KEFCore: Getting started

To use [Entity Framework Core](https://learn.microsoft.com/it-it/ef/core/) provider for [Apache Kafka](https://kafka.apache.org/) you must have at least:
- an installed JRE/JDK (11+)
- an accessible Apache Kafka broker (a full cluster or a local Dockerized version)

> IMPORTANT NOTE: till the first major version, all releases shall be considered not stable: this means the API public, or internal, can be change without notice.

## First project setup

- Create a new simple empty project:

```pwsh
dotnet new console
```

- Entity Framework Core provider for Apache Kafka is available on [NuGet](https://www.nuget.org/packages/MASES.EntityFrameworkCore.KNet). Execute the following command to add the package to the newly created project:

```pwsh
dotnet add package MASES.EntityFrameworkCore.KNet
```

- Edit Program.cs and replace the content with the following code:

```c#
using MASES.EntityFrameworkCore.KNet.Infrastructure;
using System.Collections.Generic;

namespace MASES.EntityFrameworkCore.KNet.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            KEFCore.CreateGlobalInstance();
            using var context = new BloggingContext()
            {
                BootstrapServers = "MY-KAFKA-BROKER:9092",
                ApplicationId = "MyAppId",
                DbName = "MyDBName",
            };
            // add standard EFCore queries
        }
    }

    public class BloggingContext : KafkaDbContext { }

    public class Blog
    {
        public int BlogId { get; set; }
        public string Url { get; set; }
        public int Rating { get; set; }
        public List<Post> Posts { get; set; }
    }

    public class Post
    {
        public int PostId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }

        public int BlogId { get; set; }
        public Blog Blog { get; set; }
    }
}
```

The previous code follows the example of https://learn.microsoft.com/en-us/ef/core/. See [KEFCore usage](usage.md) and [KafkaDbContext](kafkadbcontext.md) to find more information.

- Build the project

```pwsh
dotnet build
```

## Environment initialization

KEFCore shall initialize the environment before any operation can be done. The initialization is done executing the following command at first stages of your application:

```c#
KEFCore.CreateGlobalInstance();
```

The previous command identify the JVM and start it, loads the needed libraries and setup the environment. Browsing the [repository](https://github.com/masesgroup/KEFCore) within the test folder there are some examples.
KEFCore accepts many command-line switches to customize its behavior, the full list is available at [Command line switch](https://masesgroup.github.io/KNet/articles/commandlineswitch.html) of KNet.

### JVM identification

One of the most important command-line switch is **JVMPath** and it is available in [JCOBridge switches](https://www.jcobridge.com/net-examples/command-line-options/): it can be used to set-up the location of the JVM library if JCOBridge is not able to identify a suitable JRE/JDK installation.
If a developer is using KEFCore within its own product it is possible to override the **JVMPath** property with a snippet like the following one:

```c#
    class MyKEFCore : KEFCore
    {
        public override string JVMPath
        {
            get
            {
                string pathToJVM = "Set here the path to JVM library or use your own search method";
                return pathToJVM;
            }
        }
    }
```

**IMPORTANT NOTE**: `pathToJVM` shall be escaped
1. `string pathToJVM = "C:\\Program Files\\Eclipse Adoptium\\jdk-11.0.18.10-hotspot\\bin\\server\\jvm.dll";`
2. `string pathToJVM = @"C:\Program Files\Eclipse Adoptium\jdk-11.0.18.10-hotspot\bin\server\jvm.dll";`


### Special initialization conditions

[JCOBridge](https://www.jcobridge.com/) try to identify a suitable JRE/JDK installation within the system using some standard mechanism of JRE/JDK: `JAVA_HOME` environment variable or Windows registry if available.
However it is possible, on Windows operating systems, that the library raises an **InvalidOperationException: Missing Java Key in registry: Couldn't find Java installed on the machine**.
This means that neither `JAVA_HOME` nor Windows registry contains information about a default installed JRE/JDK: some vendors may not setup them.
If the developer/user encounter this condition can do the following steps:
1. On a command prompt execute `set | findstr JAVA_HOME` and verify the result;
2. If something was reported maybe the `JAVA_HOME` environment variable is not set at system level, but at a different level like user level which is not visible from the KEFCore process that raised the exception;
3. Try to set `JAVA_HOME` at system level e.g. `JAVA_HOME=C:\Program Files\Eclipse Adoptium\jdk-11.0.18.10-hotspot\`;
4. Try to set `JCOBRIDGE_JVMPath` at system level e.g. `JCOBRIDGE_JVMPath=C:\Program Files\Eclipse Adoptium\jdk-11.0.18.10-hotspot\`.

**IMPORTANT NOTES**:
- One of `JCOBRIDGE_JVMPath` or `JAVA_HOME` environment variables or Windows registry (on Windows OSes) shall be available
- `JCOBRIDGE_JVMPath` environment variable takes precedence over `JAVA_HOME` and Windows registry: you can set `JCOBRIDGE_JVMPath` to `C:\Program Files\Eclipse Adoptium\jdk-11.0.18.10-hotspot\bin\server\jvm.dll` and avoid to override `JVMPath` in your code
- After first initialization steps, `JVMPath` takes precedence over `JCOBRIDGE_JVMPath`/`JAVA_HOME` environment variables or Windows registry