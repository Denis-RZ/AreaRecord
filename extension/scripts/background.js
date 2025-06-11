chrome.runtime.onInstalled.addListener(()=>{
  if (command === 'toggle-recording-pro') {
    chrome.tabs.query({active:true,currentWindow:true}, tabs=>{
      if (tabs[0]) chrome.tabs.sendMessage(tabs[0].id, {sarProStart:true});
    });
  }
});

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (msg && msg.openDriveAuth) {
    chrome.tabs.create({ url: chrome.runtime.getURL('html/drive-auth.html') });
    sendResponse({ok:true});
  }
});

// Handle frame-specific messages
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message.type === 'sar-update-area' && message.area) {
    // Store frame information for the recording
    chrome.tabs.sendMessage(sender.tab.id, {
      type: 'sar-frame-update',
      area: message.area
    });
  }
});

// Enhanced recording initialization for frames
async function initializeRecording(tabId, frameInfo) {
  const recording = {
    tabId,
    frameInfo,
    startTime: Date.now()
  };
  
  // Store recording state
  activeRecordings.set(tabId, recording);
  
  // Notify content script to start frame-aware recording
  chrome.tabs.sendMessage(tabId, {
    type: 'sar-start-recording',
    includeFrame: true
  });
}