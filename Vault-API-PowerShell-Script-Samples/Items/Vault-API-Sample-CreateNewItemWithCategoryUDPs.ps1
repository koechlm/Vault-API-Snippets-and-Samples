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

#Reference Item Service
$ItemSvc = $vault.ItemService

#Get target item category Id
$mEntityCategories = $vault.CategoryService.GetCategoriesByEntityClassId("ITEM", $true)
$mEntCatId = ($mEntityCategories | Where-Object { $_.Name -eq "General" }).ID
$mEntCatId2 = ($mEntityCategories | Where-Object { $_.Name -eq "Part" }).ID

#Create new item and commit (Step 1)
[Autodesk.Connectivity.WebServices.Item]$NewItem = $ItemSvc.AddItemRevision($mEntCatId)
$NewItem.Title = "My New Item"
$NewItem.Detail = "2 steps: create, update category"
$NewItem.Comm = "initial item, no cat properties added"
#save the item;
$ItemSvc.UpdateAndCommitItems(@($NewItem))
[Autodesk.Connectivity.WebServices.Item]$ItemResult = $ItemSvc.GetLatestItemByItemMasterId($NewItem.MasterId)
#edit the new item to add category properties (Step 2)
[Autodesk.Connectivity.WebServices.Item]$EditItem = $ItemSvc.EditItems(@($ItemResult.RevId))[0]
#the UpdateItemCategories includes the commit
$NewItem = ($ItemSvc.UpdateItemCategories(@($EditItem.MasterId), @($mEntCatId2), "New Item with UDPs for Part Category"))[0]
            			
#endregion ExecuteInVault
		
$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can log out: $cred.SignOut($vault.AuthService, $vault.WinAuthService)


#endregion ConnectToVault