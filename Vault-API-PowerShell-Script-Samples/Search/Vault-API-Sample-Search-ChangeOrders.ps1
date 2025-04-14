#region disclaimer
#===============================================================================#
# PowerShell script sample														#
# Author: Markus Koechl															#
# Copyright (c) Autodesk 2025													#
#																				#
# THIS SCRIPT/CODE IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER     #
# EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES   #
# OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, OR NON-INFRINGEMENT.    #
#===============================================================================#
#endregion

#region ConnectToVault
# NOTE - click licensing v9 requires to copy AdskLicensingSDK_9.dll, AdskIdentitySDK.config, and AdskIdentitySDK.dll to PowerShell execution folder C:\Windows\System32\WindowsPowerShell\v1.0 before Powershell runtime starts

[System.Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Autodesk Vault 2026 SDK\bin\x64\Autodesk.Connectivity.WebServices.dll')
$serverID = New-Object Autodesk.Connectivity.WebServices.ServerIdentities
$serverID.DataServer = "<ServerName or IP>"
$serverID.FileServer = "<ServerName or IP>"
$VaultName = "<Name of Vault>"
$UserName = "<User Name>"
$password = "<Password>"
#Select license type by licensing agent enum "Client" (=Named User) "Server" (= (legacy) Multi-User) or "None" (=readonly access)
$licenseAgent = [Autodesk.Connectivity.WebServices.LicensingAgent]::Client
		
$cred = New-Object Autodesk.Connectivity.WebServicesTools.UserPasswordCredentials($serverID, $VaultName, $UserName, $password, $licenseAgent)
$vault = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($cred)

#region ExecuteInVault

#build the search conditions first
$mSearchString = "*"
$srchCond = New-Object autodesk.Connectivity.WebServices.SrchCond
$propDefs = $vault.PropertyService.GetPropertyDefinitionsByEntityClassId("CO")
$propDef = $propDefs | Where-Object { $_.DispName -eq "Name" }
$srchCond.PropDefId = $propDef.Id
$srchCond.SrchOper = 3
$srchCond.SrchTxt = $mSearchString
$srchCond.PropTyp = [Autodesk.Connectivity.WebServices.PropertySearchType]::SingleProperty
$srchCond.SrchRule = [Autodesk.Connectivity.WebServices.SearchRuleType]::Must
$srchSort = New-Object autodesk.Connectivity.WebServices.SrchSort
$searchStatus = New-Object autodesk.Connectivity.WebServices.SrchStatus
$bookmark = ""     
$mResultAll = New-Object 'System.Collections.Generic.List[Autodesk.Connectivity.WebServices.ChangeOrder]'
	
while (($searchStatus.TotalHits -eq 0) -or ($mResultAll.Count -lt $searchStatus.TotalHits)) {
	$mResultPage = $vault.ChangeOrderService.FindChangeOrdersBySearchConditions(@($srchCond), @($srchSort), [ref]$bookmark, [ref]$searchStatus)
			
	If ($searchStatus.IndxStatus -ne "IndexingComplete" -or $searchStatus -eq "IndexingContent") {
		#check the indexing status; you might return a warning that the result bases on an incomplete index, or even return with a stop/error message, that we need to have a complete index first
	}
	if ($mResultPage.Count -ne 0) {
		$mResultAll.AddRange($mResultPage)
	}
	else { break; }
				
	break; #limit the search result to the first result page; page scrolling not implemented in this snippet release
}
		
#endregion ExecuteInVault

$vault.Dispose() #don't forget to release the connection


#endregion ConnectToVault