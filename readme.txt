===============================================================================
Introduction
===============================================================================

Spark is a research shading language that aims to improve support for
separation of concerns in real-time shaders.

This package includes source code for the command-line Spark
compiler (sparkc) and runtime (Spark.dll and SparkCPP.dll), along with
example programs that can be used to compare Spark and HLSL shaders.

Contents:
- Prerequisites
- Documentation
- Running the Example Programs
- Using sparkc
- Building
- Known Issues

===============================================================================
Prerequisites
===============================================================================

In order to run the included example binaries, you will need:

- Windows 7 or Vista
  - Spark-generated shaders use the Direct3D 11 interface
- Microsoft .NET Framework version 4.0

===============================================================================
Documentation
===============================================================================

An introduction to the Spark language and a reference to the Spark interface
for Direct3D 11 can be found in:
spark/docs/Spark User's Guide.pdf

License information for Spark can be found in license.txt.

===============================================================================
Building
===============================================================================

Information on how to build the Spark compiler, runtime or examples from
source can be found in the file building.txt.

===============================================================================
Running the Example Programs
===============================================================================

The example programs are two Direct3D 11 samples from the DirectX SDK:
BasicHLSL11 and PNTriangles11. These examples have been ported to
support Spark shaders in addition to HLSL.

To launch the examples, build Spark and use one of the executables:

spark/examples/Direct3D11/BasicHLSL11/bin/x86/Release/BasicHLSL11.exe
spark/examples/Direct3D11/CubeMapGS11/bin/x86/Release/CubeMapGS11.exe
spark/examples/Direct3D11/PNTriangles11/bin/x86/Release/PNTriangles11.exe

Upon running these executables you should see a typical DX SDK example
window. The "Use Spar(k)" checkbox may be used to switch between Spark
and HLSL shading.

More information on the examples can be found in spark/examples/about.txt.

===============================================================================
Using sparkc
===============================================================================

The command-line Spark compiler does not currently include any command-line
option processing, so it must be used in exactly the following way:

    sparkc -o <prefix> <file>

This will compile the file containing Spark code, emitting any diagnostic
messages (warnings, errors) to standard error. It will generate two
C++ files, <prefix>.h and <prefix>.cpp, which provide a wrapper interface
to the shader classes defined in the file. Any non-abstract shader
class in the file will also have shader bytecode and C++ submission
functions generated.

===============================================================================
Known Issues
===============================================================================

Known issues with the Spark compiler and runtime are detailed in the file
issues.txt.

===============================================================================
Legal
===============================================================================

License information for Spark can be found in license.txt.

This software is subject to the U.S. Export Administration Regulations and
other U.S. law, and may not be exported or re-exported to certain countries
(Burma, Cuba, Iran, North Korea, Sudan, and Syria) or to persons or entities
prohibited from receiving U.S. exports (including Denied Parties, Specially
Designated Nationals, and entities on the Bureau of Export Administration
Entity List or involved with missile technology or nuclear, chemical or
biological weapons).

