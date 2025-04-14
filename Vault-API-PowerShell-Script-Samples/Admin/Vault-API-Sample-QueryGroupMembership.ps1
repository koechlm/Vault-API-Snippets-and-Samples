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
$VaultName = "<Name of Vault>"
$UserName = "<User Name>"
$password = "<Password>"
#Select license type by licensing agent enum "Client" (=Named User) "Server" (= (legacy) Multi-User) or "None" (=readonly access)
$licenseAgent = [Autodesk.Connectivity.WebServices.LicensingAgent]::Client
		
$cred = New-Object Autodesk.Connectivity.WebServicesTools.UserPasswordCredentials($serverID, $VaultName, $UserName, $password, $licenseAgent)
$vault = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($cred)

#region ExecuteInVault
		
#query data, create folder(s)...
$mGroupInfo = New-Object Autodesk.Connectivity.WebServices.GroupInfo
$mGroup = $vault.AdminService.GetGroupByName("Configuration Administrators")
$mGroupInfo = $vault.AdminService.GetGroupInfoByGroupId($mGroup.Id)
foreach ($user in $mGroupInfo.Users) {
	if ($vault.AdminService.Session.User.Id -eq $user.Id) {				
		echo "Current user is allowed to proceed"
	}
}
            

#endregion ExecuteInVault

$vault.Dispose() #don't forget to release the connection


#endregion ConnectToVault