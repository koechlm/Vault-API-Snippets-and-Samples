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
Try {
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
	Try {
		[Autodesk.Connectivity.WebServices.JobParam[]] $mJobParams = @()
		$mJobParamFile = New-Object Autodesk.Connectivity.WebServices.JobParam
				
		$mJobParamFile.Name = "FileMasterId"
		$mJobParamFile.Val = "15030"
		$mJobParams += $mJobParamFile

		[System.Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Autodesk Vault 2026 SDK\bin\x64\Explorer\Autodesk.DataManagement.Client.Framework.Vault.dll')
		[System.Enum]$AuthFlag = [Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.AuthenticationFlags]::Standard
		$UsrID = $vault.AdminService.Session.User.Id			
		$vaultConnection = New-Object Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.Connection($vault, $VaultName, $UsrID, $serverID, $AuthFlag )
		$vaultConnection.WebServiceManager.JobService.AddJob("Autodesk.Vault.ExtractBOM.Inventor", "Extract Item Data: manually added Master ID 15030.", $mJobParams, 1)
		Write-Output "Job successfully added to Queue"
	}
	Catch {
		$ErrorMessage = $_.Exception.Message
		Write-Output $ErrorMessage
	}
	#endregion ExecuteInVault
}
Catch {
	$ErrorMessage = $_.Exception.Message
	Write-Output $ErrorMessage
}
#endregion ConnectToVault

#region DisconnectVault - don't forget ;)
Finally {
	$vaultConnection = $null
	$vault.Dispose()
}
#endregion DisconnectVault