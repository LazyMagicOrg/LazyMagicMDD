
param([string]$ProjectDir)

function Format-Xml {
    param(
        [Parameter(Mandatory=$true)]
        [xml]$xml
    )
    
    $stringWriter = New-Object System.IO.StringWriter
    $xmlWriter = New-Object System.Xml.XmlTextWriter($stringWriter)
    
    # Configure the XmlTextWriter for pretty formatting
    $xmlWriter.Formatting = "Indented"
    $xmlWriter.Indentation = 2
    $xmlWriter.IndentChar = ' '
    
    # Preserve whitespace if the xml:space attribute is set to "preserve"
    $xml.PreserveWhitespace = $true
    $xml.WriteTo($xmlWriter)
    
    $xmlWriter.Flush()
    $stringWriter.Flush()
    return $stringWriter.ToString()
}


# Load the Version file
try {
$xmlVersion = [xml](Get-Content "$ProjectDir..\Version.props")
$version = $xmlVersion.SelectSingleNode("//Project/PropertyGroup/Version").InnerText
Write-Host "Version: $version"
} catch {
    Write-Host "Error loading vesion file."
    exit 

}
# Load the XML file
$xmlpath = $ProjectDir + "source.extension.vsixmanifest"
try {
    $xml = [xml](Get-Content $xmlpath)
    # Write-Host ($xml.OuterXml)

} catch {
    Write-Host "Error loading XML file."
     Write-Host "Error Message: $($_.Exception.Message)"
    exit 
}



# Find the element
$ns = New-Object Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace("ns", "http://schemas.microsoft.com/developer/vsx-schema/2011")
$xPath = "//ns:Identity"
$element = $xml.SelectSingleNode($xPath, $ns)

# $element = $xml.SelectSingleNode("//PackageManifest/Metadata/Identity")

if ($element -ne $null) {
    # Modify the property
    $identityVersion = $element.GetAttribute("Version")
    Write-Host "Element found in the XML file. $identityVersion"
    $element.Version = $version
    # Save the changes back to the file
    try {
        #$xml.Save($xmlpath)

        Write-Host "Attempting to save using Set-Content..."

        #$xml.OuterXml | Set-Content $xmlpath -Encoding UTF8

        $formattedXml = Format-Xml -xml $xml
        $formattedXml | Set-Content $xmlpath -Encoding UTF8

                
    } catch {
        Write-Host "XML file not updated."
        Write-Host "Error Message: $($_.Exception.Message)"
        exit 
    }
    Write-Host "XML file updated successfully."
} else {
    Write-Host "Element not found in the XML file."
}