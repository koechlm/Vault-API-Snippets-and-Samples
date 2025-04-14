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
		[System.Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Autodesk Vault 2026 SDK\bin\x64\Autodesk.DataManagement.Client.Framework.Vault.dll')
		[System.Enum]$AuthFlag = [Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.AuthenticationFlags]::Standard
		$UsrID = $vault.AdminService.Session.User.Id		
		$vaultConnection = New-Object Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections.Connection($vault, $VaultName, $UsrID, $serverID, $AuthFlag )
		$mSchedJobs = $vaultConnection.WebServiceManager.JobService.GetScheduledJobs()
		#Filter Scheduled Jobs according current Vault
		$mVaultId = ($vault.FileStoreVaultService.GetKnowledgeVaultByName($VaultName)).Id
		$mSchedJobs = $mSchedJobs | Where-Object { $_.VaultId -eq $mVaultId } 
				
		$mSchedJobSelected = $mSchedJobs[0]

		$mJobMaxCount = 100
		$mJobStartDate = Get-Date -Day 01 -Month 01 -Year 2010
		$mJobList = $vaultConnection.WebServiceManager.JobService.GetJobsByDate($mJobMaxCount, $mJobStartDate)
				
		$mSchedJobParam1 = New-Object Autodesk.Connectivity.WebServices.JobParam
		$mSchedJobParams = @()
		$mSchedJobParam1.Name = $mSchedJobSelected.ParamArray[0].Name
		$mSchedJobParam1.Val = $mSchedJobSelected.ParamArray[0].Val
		$mSchedJobParams += $mSchedJobParam1
		$mSchedJobParam2 = New-Object Autodesk.Connectivity.WebServices.JobParam
		$mSchedJobParam2.Name = $mSchedJobSelected.ParamArray[1].Name
		$mSchedJobParam2.Val = $mSchedJobSelected.ParamArray[1].Val
		$mSchedJobParams += $mSchedJobParam2
		$mSchedJobId = $mSchedJobSelected.Id

		$mJobParams = @()
		$mJobParamSyncJob1 = New-Object Autodesk.Connectivity.WebServices.JobParam
		$mJobParamSyncJob1.Name = $mSchedJobParams[0].Name
		$mJobParamSyncJob1.Val = $mSchedJobParams[0].Val
		#add Param[0]
		$mJobParams += $mJobParamSyncJob1
				
		$mJobParamSyncJob2 = New-Object Autodesk.Connectivity.WebServices.JobParam
		$mJobParamSyncJob2.Name = $mSchedJobParams[1].Name
		$mJobParamSyncJob2.Val = $mSchedJobParams[1].Val
		#add Param[1]
		$mJobParams += $mJobParamSyncJob2

		$mJobParamSyncJob3 = New-Object Autodesk.Connectivity.WebServices.JobParam
		$mJobParamSyncJob3.Name = "ScheduledJobID"
		$mJobParamSyncJob3.Val = $mSchedJobId
		#add Param[2]
		$mJobParams += $mJobParamSyncJob3

		$vaultConnection.WebServiceManager.JobService.AddJob("Autodesk.Vault.Sync.ScheduledUpload", "Autodesk.Vault.Sync.ScheduledUpload", $mJobParams, 1)
		echo "Job successfully added to Queue"
	}
	Catch {
		$ErrorMessage = $_.Exception.Message
		echo $ErrorMessage
	}
	#endregion ExecuteInVault
}
Catch {
	$ErrorMessage = $_.Exception.Message
	echo $ErrorMessage
}
#endregion ConnectToVault

#region DisconnectVault - don't forget ;)
Finally {
	$vaultConnection = $null
	$vault.Dispose()
}
#endregion DisconnectVault