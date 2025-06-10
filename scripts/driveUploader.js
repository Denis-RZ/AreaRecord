// driveUploader.js - Fixed OAuth implementation
export async function uploadToDrive(blob, filename) {
  // Try to get an existing token first
  let token = await getAuthToken();
  
  // If no token, try to initiate the auth flow
  if (!token) {
    try {
      token = await initiateAuthFlow();
    } catch (error) {
      // If regular flow fails, try manual token input as last resort
      token = await manualTokenInput();
    }
  }
  
  if (!token) {
    throw new Error('Drive authentication failed');
  }
  
  // Get or create the target folder
  const config = await getFolderConfig();
  let folderId = await getOrCreateFolder(token, config.folder, config.organize);
  
  // Upload the file
  return await uploadFileToDrive(token, blob, filename, folderId);
}

// Fixed manual token input function
async function manualTokenInput() {
  return new Promise((resolve) => {
    const token = prompt(
      'Enter Google OAuth access_token\n' +
      '(You can get it from https://developers.google.com/oauthplayground)\n' +
      'Select Drive API v3 scope and click "Exchange authorization code for tokens"'
    );
    
    if (!token) {
      resolve(null);
      return;
    }
    
    // Store the token
    const tokenData = {
      access_token: token.trim(),
      expiry: Date.now() + 3600000 // 1 hour
    };
    
    chrome.storage.local.set({ googleAuthToken: tokenData });
    resolve(token.trim());
  });
}

// Get configuration for folder
async function getFolderConfig() {
  const { sarPro } = await chrome.storage.sync.get(['sarPro']);
  return {
    folder: sarPro?.driveFolder || 'Screen Recordings',
    organize: sarPro?.autoOrganize || false
  };
}

// Get or create the target folder
async function getOrCreateFolder(token, folderName, organize) {
  // If organize by date is enabled, use a structure like "Screen Recordings/2025/05"
  if (organize) {
    const date = new Date();
    const year = date.getFullYear().toString();
    const month = (date.getMonth() + 1).toString().padStart(2, '0');
    
    // First find or create the base folder
    const baseId = await findOrCreateFolder(token, folderName, 'root');
    
    // Then the year folder
    const yearId = await findOrCreateFolder(token, year, baseId);
    
    // Finally the month folder
    return await findOrCreateFolder(token, month, yearId);
  } else {
    // Just use the base folder
    return await findOrCreateFolder(token, folderName, 'root');
  }
}

// Find or create a folder
async function findOrCreateFolder(token, folderName, parentId) {
  // First, try to find the folder
  const query = `name='${folderName}' and mimeType='application/vnd.google-apps.folder' and '${parentId}' in parents and trashed=false`;
  const response = await fetch(`https://www.googleapis.com/drive/v3/files?q=${encodeURIComponent(query)}`, {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  
  if (!response.ok) {
    throw new Error(`Drive API error: ${response.status}`);
  }
  
  const data = await response.json();
  
  // If found, return its ID
  if (data.files && data.files.length > 0) {
    return data.files[0].id;
  }
  
  // Otherwise, create it
  const metadata = {
    name: folderName,
    mimeType: 'application/vnd.google-apps.folder',
    parents: [parentId]
  };
  
  const createResponse = await fetch('https://www.googleapis.com/drive/v3/files', {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(metadata)
  });
  
  if (!createResponse.ok) {
    throw new Error(`Failed to create folder: ${createResponse.status}`);
  }
  
  const folder = await createResponse.json();
  return folder.id;
}

// Upload file to Drive
async function uploadFileToDrive(token, blob, filename, folderId) {
  const metadata = { 
    name: filename, 
    mimeType: blob.type,
    parents: [folderId]
  };
  
  // Using FormData for more reliable uploads
  const formData = new FormData();
  formData.append('metadata', new Blob([JSON.stringify(metadata)], { type: 'application/json' }));
  formData.append('file', blob);
  
  const response = await fetch('https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart', {
    method: 'POST',
    headers: { 'Authorization': `Bearer ${token}` },
    body: formData
  });
  
  if (!response.ok) {
    throw new Error(`Upload failed: ${response.status}`);
  }
  
  return await response.json();
}

// Check for existing auth token
async function getAuthToken() {
  const { googleAuthToken } = await chrome.storage.local.get(['googleAuthToken']);
  
  if (googleAuthToken && googleAuthToken.expiry > Date.now() + 300000) {
    return googleAuthToken.access_token;
  }
  
  return null;
}

// Improved OAuth flow with popup window
export async function initiateAuthFlow() {
  return new Promise(async (resolve, reject) => {
    // Prepare the auth URL with a redirect to a page we can control
    // Получаем client_id из настроек расширения (chrome.storage.sync)
    const { sarPro } = await chrome.storage.sync.get(['sarPro']);
    const clientId = sarPro?.driveClientId || '';
    if (!clientId) {
      alert('Укажите свой Google Client ID в настройках расширения!');
      throw new Error('Google Client ID не указан');
    }
    const redirectUri = chrome.runtime.getURL('html/oauth-callback.html');
    console.log('[DriveUploader] redirectUri (добавьте в Google Cloud Console!):', redirectUri);
    const authUrl = 'https://accounts.google.com/o/oauth2/auth?' + 
      new URLSearchParams({
        client_id: clientId,
        redirect_uri: redirectUri,
        response_type: 'token',
        scope: 'https://www.googleapis.com/auth/drive.file',
        prompt: 'consent'
      });
    console.log('[DriveUploader] authUrl:', authUrl);
    
    // Try to use chrome.identity API if available (more reliable)
    if (chrome.identity && chrome.identity.launchWebAuthFlow) {
      chrome.identity.launchWebAuthFlow({
        url: authUrl,
        interactive: true
      }, (responseUrl) => {
        if (chrome.runtime.lastError) {
          reject(new Error(chrome.runtime.lastError.message));
          return;
        }
        
        if (!responseUrl) {
          reject(new Error('Authorization failed'));
          return;
        }
        
        const url = new URL(responseUrl);
        const hashParams = new URLSearchParams(url.hash.substring(1));
        const accessToken = hashParams.get('access_token');
        const expiresIn = hashParams.get('expires_in') || 3600;
        
        if (accessToken) {
          const tokenData = {
            access_token: accessToken,
            expiry: Date.now() + (parseInt(expiresIn) * 1000)
          };
          
          chrome.storage.local.set({ googleAuthToken: tokenData });
          resolve(accessToken);
        } else {
          reject(new Error('No access token in response'));
        }
      });
    } else {
      // Fallback to manual popup window
      const popupWidth = 500;
      const popupHeight = 600;
      const left = window.screenX + (window.outerWidth - popupWidth) / 2;
      const top = window.screenY + (window.outerHeight - popupHeight) / 2;
      
      const popup = window.open(
        authUrl,
        'google-auth',
        `width=${popupWidth},height=${popupHeight},left=${left},top=${top}`
      );
      
      if (!popup) {
        reject(new Error('Popup blocked. Please allow popups and try again.'));
        return;
      }
      
      // Poll for changes in URL
      const pollTimer = setInterval(() => {
        try {
          if (popup.closed) {
            clearInterval(pollTimer);
            reject(new Error('Authentication window closed'));
            return;
          }
          
          const currentUrl = popup.location.href;
          
          if (currentUrl.includes('access_token=')) {
            clearInterval(pollTimer);
            
            const hashParams = new URLSearchParams(currentUrl.split('#')[1]);
            const accessToken = hashParams.get('access_token');
            const expiresIn = hashParams.get('expires_in') || 3600;
            
            if (accessToken) {
              popup.close();
              
              const tokenData = {
                access_token: accessToken,
                expiry: Date.now() + (parseInt(expiresIn) * 1000)
              };
              
              chrome.storage.local.set({ googleAuthToken: tokenData });
              resolve(accessToken);
            } else {
              popup.close();
              reject(new Error('No access token in response'));
            }
          } else if (currentUrl.includes('error=')) {
            clearInterval(pollTimer);
            popup.close();
            reject(new Error('Authorization denied'));
          }
        } catch (e) {
          // CORS exceptions are expected when checking cross-origin URLs
          // Just ignore them and keep polling
        }
      }, 500);
      
      // Set a timeout to prevent infinite polling
      setTimeout(() => {
        clearInterval(pollTimer);
        if (!popup.closed) {
          popup.close();
        }
        reject(new Error('Authentication timed out'));
      }, 120000); // 2 minutes
    }
  });
}

// For testing connection
export async function testDriveConnection() {
  let token = await getAuthToken();
  
  if (!token) {
    token = await initiateAuthFlow();
  }
  
  if (!token) {
    throw new Error('Authentication failed');
  }
  
  const response = await fetch('https://www.googleapis.com/drive/v3/about?fields=user', {
    headers: { 'Authorization': `Bearer ${token}` }
  });
  
  if (!response.ok) {
    if (response.status === 401) {
      // Token expired, clear it
      chrome.storage.local.remove(['googleAuthToken']);
      throw new Error('Token expired');
    }
    throw new Error(`HTTP ${response.status}: ${response.statusText}`);
  }
  
  const data = await response.json();
  return data.user;
}