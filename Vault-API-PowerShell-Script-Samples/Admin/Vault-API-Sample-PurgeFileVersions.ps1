#region disclaimer
#===================================================================
# PowerShell script sample													
# Author: Markus Koechl															
# Copyright (c) Autodesk 2025													
#																				
# THIS SCRIPT/CODE IS PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, EITHER     
# EXPRESSED OR IMPLIED, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES   
# OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, OR NON-INFRINGEMENT.    
#===================================================================
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
#sample to purge lfc controlled file:
$mCadFiles = $vault.DocumentService.FindLatestFilesByPaths(@("$/Designs/FileToPurge.ipt"))
$mCadFile = $mCadFiles[0]
$mPurgeResult = $vault.DocumentService.DeleteFileVersionsByMasterIds(@($mCadFile.MasterId), $false, $null, $null, $null)			
			
#sample to purge un-controlled file
$mBaseFiles = $vault.DocumentService.FindLatestFilesByPaths(@("$/Designs/FileToPurge.txt"))
$mBaseFile = $mBaseFiles[0]
$mPurgeResult = $vault.DocumentService.DeleteFileVersionsByMasterIds(@($mBaseFile.MasterId), $true, 1, 1, "exclude comment")
			
#endregion ExecuteInVault

$vault.Dispose() #don't forget to release the connection
#endregion ConnectToVault