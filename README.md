# LazyMagicMDD 
This solution generates two LazyMagic Generator projects:
- LazyMagicApp - dotnet command line tool
- LazyMagicExt - Visual Studio extension 

## Version 3.x.x
Added Module Support 

## Version Management
We have a Version.props file in the solution folder that tracks the latest version number of the solution projects.
The LazyMagicCLI project csproj file imports the Version.props file to get the version number.
The LazyMagicVsExt project needs speical handling. We have a script that:
	1. Load the Version.props file and extracts the current version string
	2. Loads the source.extension.vsixmanifest file and updates the version string in that file to match the one in Vesion.props

We have a publish-nuget.yaml file that defines the action taken on push to the master branch. We create a new tag with the version number
drawn from the version.props file.

