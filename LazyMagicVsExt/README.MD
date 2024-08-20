# README

## Signing Vsix File
- Create LazyMagicCertificate folder (same level as LazyMagicGen folder)
- Get a OV Code Signing Certificate from ssl.com -- this can take a week for new cert
- Place certificate in LazyMagicCertificate folder
- CD into folder
- certutil -dump certfilename
- Get the Cert Hash of the certificate (usually the last one in the file)
- Create a CertPassword.csproj file
```xml
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <PropertyGroup>
        <SIGN_PASSWORD>xxxxxxxx</SIGN_PASSWORD>
        <SIGN_CERT_HASH>xxxxxxxxxxxxxxxxxxxxxx</SIGN_CERT_HASH>
    </PropertyGroup>
</Project>
```
The contents of the LazyMagicCertificate folder IS NOT Commited to GitHub!

Install certificate in Windows 10 - https://www.ssl.com/how-to/install-ov-code-signing-certificates/

Install https://github.com/vcsjones/OpenOpcSignTool

The LazyMagicVsExt.csproj file handles the rest.

Note: The vsix file is only signed for release builds.

## Updating the ProjectTemplate
- https://docs.microsoft.com/en-us/visualstudio/ide/how-to-create-project-templates?view=vs-2022 
- copy the project template into the LazyMagicVsExt/ProjectTemplates folder

## Releasing a new version
- update the version in the source.extensions.vsixmanifest file
- https://marketplace.visualstudio.com/manage/publishers/insightsciencescorporation 
- Click on the '...' and select edit
- Upload new vsix file from bin/release
- Click Save & Upload

