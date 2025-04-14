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
#Select license type by licensing agent enum "Client" (=Named User) "Server" (= (legacy) Multi-User) or "None" (=readonly access) or "Token" (Autodesk ID)
$licenseAgent = [Autodesk.Connectivity.WebServices.LicensingAgent]::Client
		
$cred = New-Object Autodesk.Connectivity.WebServicesTools.UserPasswordCredentials($serverID, $VaultName, $UserName, $password, $licenseAgent)
$vault = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($cred)

#region ExecuteInVault

# in order to run the sample code, you need to identify a file linked to an item and controlled by a change order
# ToDo: change the file path in line 36 accordingly

#get the folder object
$FolderFullPath = "$/Designs/SR-0006/Discharge Chute"
$folder = $vault.DocumentService.GetFolderByPath($FolderFullPath)
#create TC link
$serverUri = [System.Uri]$Vault.InformationService.Url		
$TcFolderLink = "$($serverUri.Scheme)://$($VaultConnection.Server)/AutodeskTC/$($VaultConnection.Vault)/explore/folder/$($folder.Id)"
#open link with default browser
Start-Process $TcFolderLink
			
#get file master	
$filePaths = @("$/Designs/SR-0006/Discharge Chute/01-0745.iam")	
$file = $vault.DocumentService.FindLatestFilesByPaths($filePaths)[0]
#create TC link
$TcFileMasterLink = "http://" + $serverID.DataServer + "/AutodeskTC/" + $VaultName + "/explore/file/" + $file.MasterId
Start-Process $TcFileMasterLink
			
#get historical file iterations
$files = @()
$files += $vault.DocumentService.GetFilesByMasterId($file.MasterId)
#create TC links for all historical versions
$files | ForEach-Object {
	$TcFileVersionLink = "http://" + $serverID.DataServer + "/AutodeskTC/" + $VaultName + "/explore/fileversion/" + $_.Id
	Start-Process $TcFileVersionLink
}

#get item of the file
$item = $vault.ItemService.GetItemsByFileId($file.Id)[0]
#create TC link
$TcItemMasterLink = "http://" + $serverID.DataServer + "/AutodeskTC/" + $VaultName + "/items/item/" + $item.MasterId
Start-Process $TcItemMasterLink

#get historical item iterations
$items = @()
$items += $vault.ItemService.GetItemHistoryByItemMasterId($item.MasterId, "All")
#create TC links for all historical item versions
$items | ForEach-Object {
	$TcItemVersionLink = "http://" + $serverID.DataServer + "/AutodeskTC/" + $VaultName + "/items/itemversion/" + $_.Id
	Start-Process $TcItemVersionLink
}

#get change order
$changeOrder = $vault.ChangeOrderService.GetChangeOrderFilesByFileMasterId($file.MasterId)[0] 
#create TC link of CO
$TcChangeOrderLink = "http://" + $serverID.DataServer + "/AutodeskTC/" + $VaultName + "/changeorders/changeorder/" + $changeOrder.ChangeOrder.Id
Start-Process $TcChangeOrderLink

#endregion ExecuteInVault
		
$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can log out: $cred.SignOut($vault.AuthService, $vault.WinAuthService)


#endregion ConnectToVault