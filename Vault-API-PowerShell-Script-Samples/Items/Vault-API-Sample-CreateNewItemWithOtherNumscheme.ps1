﻿#region disclaimer
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
$mEntCatId = ($mEntityCategories | Where-Object { $_.Name -eq "Document" }).ID

#Create new item and commit
[Autodesk.Connectivity.WebServices.Item]$NewItem = $ItemSvc.AddItemRevision($mEntCatId)
$NewItem.Title = "Reserved Item Number"
$NewItem.Detail = "Generated by VDS Quickstart - Reserve Numbers"
$NewItem.Comm = "Item to be consumed matching Equivalence Value"
#apply a specific item numbering scheme
[Autodesk.Connectivity.WebServices.ProductRestric]$mRstrct
#$mRstrct.ParamArray = @()
$mRestrictions = @($mRstrct)
			
[Autodesk.Connectivity.WebServices.StringArray]$mFldInpt = new-object Autodesk.Connectivity.WebServices.StringArray
			
[Autodesk.Connectivity.WebServices.StringArray[]]$mInputs = new-object Autodesk.Connectivity.WebServices.StringArray
			
			
$mSchmId = 2
$mNmbrs = @()
$mNmbrs += $ItemSvc.AddItemNumbers(@($NewItem.MasterId), @($mSchmId), $mInputs, [ref]$mRestrictions)
$NewItem.ItemNum = $mNmbrs[0].ItemNum1
$ItemSvc.CommitItemNumbers(@($NewItem.MasterId), @($mNmbrs[0].ItemNum1))
#save the item or skip saving it in case you consume a number only.
$ItemSvc.UpdateAndCommitItems(@($NewItem))
			
			
$ItemSvc.DeleteUnusedItemNumbers(@($NewItem.MasterId))
#endregion ExecuteInVault
		
$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can log out: $cred.SignOut($vault.AuthService, $vault.WinAuthService)


#endregion ConnectToVault