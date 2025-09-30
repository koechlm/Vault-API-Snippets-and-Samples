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
		# PowerShell 5.1: consume libraries from Vault SDK .\bin\x64 folder
		# PowerShell 7.x: consume libraries from Vault SDK .\bin\x64\Core
		#	copy required dlls to PowerShell execution folder C:\Program Files\PowerShell\7\ (or wherever you installed PowerShell 7.x)
		# 	add the Ijwhost.dll to the folder as well

		# PowerShell 5.1
		[System.Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Autodesk Vault 2026 SDK\bin\x64\Autodesk.Connectivity.WebServices.dll')
		# PowerShell 7.x
		# [System.Reflection.Assembly]::LoadFrom('C:\Program Files\Autodesk\Autodesk Vault 2026 SDK\bin\x64\Core\Autodesk.Connectivity.WebServices.dll')

		$serverID = New-Object Autodesk.Connectivity.WebServices.ServerIdentities
			$serverID.DataServer = "<ServerName or IP>"
			$serverID.FileServer = "<ServerName or IP>"
		$VaultName = "<Name of Vault>"

		#Select license type by licensing agent enum "Client" (=Named User) "Server" (= (legacy) Multi-User) or "None" (=readonly access) or "Token" (Autodesk ID)
		$licenseAgent = [Autodesk.Connectivity.WebServices.LicensingAgent]::Client
		
		# create Windows Authentication credential object
		[Autodesk.Connectivity.WebServicesTools.WinAuthCredentialsWinAuthCredentials] $winCred = New-Object Autodesk.Connectivity.WebServicesTools.WinAuthCredentials($serverID, $VaultName, $null, $licenseAgent)
		$vault = New-Object Autodesk.Connectivity.WebServicesTools.WebServiceManager($winCred)

		#region ExecuteInVault

		$_UserId = $vault.AuthService.Session.User.Id
		[Autodesk.Connectivity.WebServices.UserInfo]$_AdminUserInfo = $vault.AdminService.GetUserInfoByUserId($_UserId)
			
		#endregion ExecuteInVault
		
		$vault.Dispose() #don't forget to release the connection, to return the (server) license you also can log out: $cred.SignOut($vault.AuthService, $vault.WinAuthService)


#endregion ConnectToVault