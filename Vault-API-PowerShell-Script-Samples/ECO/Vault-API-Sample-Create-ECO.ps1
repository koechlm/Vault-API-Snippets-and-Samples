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
$serverID.DataServer = "localhost"
$serverID.FileServer = "localhost"
$VaultName = ""
$UserName = ""
$password = ""
#Select license type by licensing agent enum "Client" (=Named User) "Server" (= (legacy) Multi-User) or "None" (=readonly access) or "Token" (Autodesk ID)
$licenseAgent = [Autodesk.Connectivity.WebServices.LicensingAgent]::Client
		
$cred = New-Object Autodesk.Connectivity.WebServicesTools.UserPasswordCredentials($serverID, $VaultName, $UserName, $password, $licenseAgent)
$vault = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($cred)

#region ExecuteInVault

# Set Reference to ChangeOrderService
$mCoSrvc = $vault.ChangeOrderService
# Get Default ECO Workflow
$mCoWflow = $mCoSrvc.GetDefaultWorkflow()
# Get Default ECO Routing
$mCoRouting = $mCoSrvc.GetRoutingsByWorkflowId($mCoWflow.Id) 
$mCoDfltRouting = $mCoRouting | Where-Object { $_.IsDflt } | Select-Object -First 1

#retrieve a new ECO number
$mCoNumSchm = @($vault.NumberingService.GetNumberingSchemes("CO", "Activated")) | Where-Object { $_.IsDflt } | Select-Object -First 1
$mCoNum = $mCoSrvc.GetChangeOrderNumberBySchemeId($mCoNumSchm.SchmID)

#set the ECO title and description
$mCoTitle = "Powershell Sample"
$mCoDescr = "Change Order Automation Sample"

#Set Due date to next month
$nextmonth = ((Get-Date).AddMonths(1))

#add entities to the Records tab; item or file master ids
$mItemIds = $null
$mFileIds = @()
if ($mFiles.Count -gt 0) {
	$mFiles | ForEach-Object {
		$mFileIds += $_.Id
	}
}

#add entities to the Files tab
$mFileAttmtsIds = $null

#$mCoProps = @(New-object Autodesk.Connectivity.WebServices.PropInst)
$mCoProps = $null
#$mCoItemAssocProps = @(New-Object Autodesk.Connectivity.WebServices.AssocPropItem)
$mCoItemAssocProps = $null
#$mCoComments = @(New-Object Autodesk.Connectivity.WebServices.MsgGroup)
$mCoComments = $null
#$mCoEmails = @(New-Object Autodesk.Connectivity.WebServices.Email)
$mCoEmails = $null

#create ChangeOrder in Vault
$ChangeOrder = $mCoSrvc.AddChangeOrder($mCoDfltRouting.Id, $mCoNum, $mCoTitle, $mCoDescr, $nextmonth, $mItemIds, $mFileAttmtsIds, $mFileIds, $mCoProps, $mCoItemAssocProps, $mCoComments, $mCoEmails)
			
#endregion ExecuteInVault
		
$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can log out: $cred.SignOut($vault.AuthService, $vault.WinAuthService)


#endregion ConnectToVault