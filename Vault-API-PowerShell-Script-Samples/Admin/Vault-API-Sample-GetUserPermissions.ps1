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
#List the client user's permissions (result of direct and indirect role and group assignments)
$mClientUserName = "Mike Manager"
Write-Output($mClientUserName + " - All Permissions:")
		
[Autodesk.Connectivity.WebServices.User]$mUser = $vault.AdminService.GetAllUsers() | Where-Object { $_.Name -eq $mClientUserName }
[Autodesk.Connectivity.WebServices.Permis[]]$mAllPermisObjects = $vault.AdminService.GetPermissionsByUserId($mUser.Id)
$mAllPermissions = @()
$mAllPermisObjects | ForEach-Object {
	$mAllPermissions += $_.Descr
	Write-Output($_.Descr)
}
Write-Output("----------------------");

#List the user's permissions given by group and group-role(s) assignment
[Long[]]$mUsrParentGroupIDs = $vault.AdminService.GetParentGroupIdsByGroupId($mUser.Id)
[Autodesk.Connectivity.WebServices.GroupInfo[]]$mGrpInfos = $vault.AdminService.GetGroupInfosByGroupIds($mUsrParentGroupIDs)
[Autodesk.Connectivity.WebServices.Permis[]]$mRolePermis = $null
		
$mGrpRolesPermissions = @{}
foreach ($mGrpInfo in $mGrpInfos) {
	if ($null -ne $mGrpInfo.Roles) {
		Write-Output("Permissions per Group '" + $mGrpInfo.Group.Name + "' and Role(s): ")
		$mRolePermissions = @{}
		foreach ($mRole in $mGrpInfo.Roles) {
			$mRolePermis = $vault.AdminService.GetPermissionsByRoleId($mRole.Id)

			foreach ($mPermis in $mRolePermis) {
				Write-Output($mRole.Name + ": " + $mPermis.Descr)
			}
			$mRolePermissions.Add($mRole.Name, $mRolePermis)
		}
		$mGrpRolesPermissions.Add($mGrpInfo.Group.Name, $mRolePermissions)
	}
}
						
#endregion ExecuteInVault
		
$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can log out: $cred.SignOut($vault.AuthService, $vault.WinAuthService)


#endregion ConnectToVault