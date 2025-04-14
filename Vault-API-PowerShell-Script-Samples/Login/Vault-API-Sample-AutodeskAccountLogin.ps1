#region disclaimer
#===============================================================================
# PowerShell script sample														
# Author: Markus Koechl															
# Copyright (c) Autodesk 2025													
#																				
# THIS SCRIPT/CODE IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER     
# EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES   
# OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, OR NON-INFRINGEMENT.    
#===============================================================================
#endregion

#region ConnectToVault

# NOTE - click licensing v9 requires to copy AdskLicensingSDK_9.dll, AdskIdentitySDK.config, and AdskIdentitySDK.dll to PowerShell execution folder C:\Windows\System32\WindowsPowerShell\v1.0 before Powershell runtime starts

[System.Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Autodesk Vault 2026 SDK\bin\x64\Autodesk.Connectivity.WebServices.dll')
$serverID = New-Object Autodesk.Connectivity.WebServices.ServerIdentities
	$serverID.DataServer = "<ServerName or IP>"
	$serverID.FileServer = "<ServerName or IP>"
$VaultName = ""

$AdskAccnt = [Autodesk.Connectivity.WebServicesTools.AutodeskAccount]::LogIn([System.IntPtr]::Zero)
$tokenId = $AdskAccnt.GetAccessToken()
$AdskAccntCred = New-Object Autodesk.Connectivity.WebServicesTools.AutodeskAuthCredentials($serverID, $VaultName, $AdskAccnt, $tokenId)
$vault = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($AdskAccntCred)

#build the search conditions first
$mSearchString = "configure.euro.dell_M3800_02.pdf"
$srchCond = New-Object autodesk.Connectivity.WebServices.SrchCond
$propDefs = $vault.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE")
$propDef = $propDefs | Where-Object { $_.SysName -eq "Name" }
$srchCond.PropDefId = $propDef.Id
$srchCond.SrchOper = 3
$srchCond.SrchTxt = $mSearchString
$srchCond.PropTyp = [Autodesk.Connectivity.WebServices.PropertySearchType]::SingleProperty
$srchCond.SrchRule = [Autodesk.Connectivity.WebServices.SearchRuleType]::Must

$mSearchStatus = New-Object autodesk.Connectivity.WebServices.SrchStatus
$srchSort = New-Object Autodesk.Connectivity.WebServices.SrchSort
#$srchSort
$mBookmark = ""     
$mResultAll = New-Object 'System.Collections.Generic.List[Autodesk.Connectivity.WebServices.File]'

while (($mSearchStatus.TotalHits -eq 0) -or ($mResultAll.Count -lt $mSearchStatus.TotalHits)) {
	$mResultPage = $vault.DocumentService.FindFilesBySearchConditions(@($srchCond), @($srchSort), @(($vault.DocumentService.GetFolderRoot()).Id), $true, $true, [ref]$mBookmark, [ref]$mSearchStatus)
	#check the indexing status; you might return a warning that the result bases on an incomplete index, or even return with a stop/error message, that we need to have a complete index first
	If ($mSearchStatus.IndxStatus -eq "IndexingComplete" -or $mSearchStatus -eq "IndexingContent") {

	}
	if ($mResultPage.Count -ne 0) {
		$mResultAll.AddRange($mResultPage)
	}
	else { break; }
		
	break; #limit the search result to the first result page; page scrolling not implemented in this snippet release
}
		
$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can l