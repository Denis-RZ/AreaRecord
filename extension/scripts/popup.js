// Clean popup.js - only essential functionality
const buildEl = document.getElementById('buildVersion');
if (buildEl) {
    const v = chrome.runtime.getManifest().version;
    buildEl.textContent = 'Build ' + v;
}

(() => {
    const $ = id => document.getElementById(id);
    
    // Main UI elements
    const btnSelect = $('btn-select');
    const statusEl = $('status');
    const areaInfo = $('area-info');
    const spanDim = $('span-dim');
    const spanPos = $('span-pos');
    const selQuality = $('quality-preset');
    
    // State variables
    let tabId = null;
    let area = null;
    let quality = selQuality?.value || '1080p';
    
    const qualitySettings = { 
        '720p': {w: 1280, h: 720, br: 2500000}, 
        '1080p': {w: 1920, h: 1080, br: 5000000}, 
        '4k': {w: 3840, h: 2160, br: 15000000}
    };

    // Status display
    function show(st, err = false) {
        if (statusEl) {
            statusEl.style.color = err ? '#c00' : '#080';
            statusEl.textContent = st;
        }
    }

    // Save settings to storage
    function save() {
        const config = { quality };
        chrome.storage.sync.set({sarPro: config});
    }

    // Load settings from storage
    function load() {
        chrome.storage.sync.get(['sarPro'], r => {
            if (r.sarPro) {
                quality = r.sarPro.quality || quality;
                if (selQuality) selQuality.value = quality;
            }
        });
    }

    // Send commands to content script
    function send(cmd, data = {}, retry = true) {
        if (!tabId) return;
        chrome.tabs.sendMessage(tabId, {cmd, ...data}, res => {
            if (chrome.runtime.lastError && retry) {
                chrome.scripting.executeScript({target: {tabId}, files: ['scripts/content.js']}, () => 
                    send(cmd, data, false)
                );
            }
        });
    }

    // Sync grid settings with active tab
    function syncGridSettingsWithTab() {
        chrome.tabs.query({active: true, currentWindow: true}, tabs => {
            if (tabs.length) {
                const tabId = tabs[0].id;
                chrome.storage.sync.get(['sarProGrid'], res => {
                    if (res && res.sarProGrid) {
                        chrome.tabs.sendMessage(tabId, { 
                            cmd: 'sar-update-grid', 
                            enabled: res.sarProGrid.enabled, 
                            step: res.sarProGrid.step 
                        });
                    }
                });
            }
        });
    }

    // Load grid settings from storage
    function loadGridSettings() {
        const gridToggle = $('popup-grid-toggle');
        const gridStepInput = $('popup-grid-step');

        if (!gridToggle || !gridStepInput) return;

        chrome.storage.sync.get(['sarProGrid'], res => {
            if (res && res.sarProGrid) {
                gridToggle.checked = !!res.sarProGrid.enabled;
                gridStepInput.value = res.sarProGrid.step || 10;
            } else {
                gridToggle.checked = false;
                gridStepInput.value = 10;
            }
            syncGridSettingsWithTab();
        });
    }

    // Save grid settings and notify content script
    function saveGridSettingsAndNotify() {
        const gridToggle = $('popup-grid-toggle');
        const gridStepInput = $('popup-grid-step');
        
        if (!gridToggle || !gridStepInput) return;

        const enabled = gridToggle.checked;
        const step = parseInt(gridStepInput.value, 10) || 10;
        
        chrome.storage.sync.set({ sarProGrid: { enabled, step } }, () => {
            chrome.tabs.query({active: true, currentWindow: true}, tabs => {
                if (tabs.length) {
                    chrome.tabs.sendMessage(tabs[0].id, { 
                        cmd: 'sar-update-grid', 
                        enabled, 
                        step 
                    });
                }
            });
        });
    }

    // Initialize on DOM ready
    window.addEventListener('DOMContentLoaded', function() {
        loadGridSettings();

        const gridToggle = $('popup-grid-toggle');
        const gridStepInput = $('popup-grid-step');

        if (gridToggle && gridStepInput) {
            gridToggle.addEventListener('change', saveGridSettingsAndNotify);
            gridStepInput.addEventListener('change', saveGridSettingsAndNotify);
            gridStepInput.addEventListener('input', saveGridSettingsAndNotify);
        }
    });

    // Message handlers
    chrome.runtime.onMessage.addListener(m => {
        if (m.type === 'sar-area-selected') {
            area = m.area;
            if (spanDim) spanDim.textContent = `${area.width}×${area.height}`;
            if (spanPos) spanPos.textContent = `(${area.x},${area.y})`;
            if (areaInfo) areaInfo.hidden = false;
            show('Area selected');
        }
        else if (m.type === 'sar-recording-started') { show('Recording…'); }
        else if (m.type === 'sar-recording-stopped') { show('Saved'); }
        else if (m.type === 'sar-error') { show(m.message, true); }
    });

    // Select area button handler
    if (btnSelect) {
        btnSelect.onclick = () => {
            chrome.tabs.query({active: true, currentWindow: true}, tabs => {
                if (!tabs.length) {
                    show('No active tab', true);
                    return;
                }
                tabId = tabs[0].id;
                chrome.scripting.executeScript({target: {tabId}, files: ['scripts/content.js']}, res => {
                    if (chrome.runtime.lastError) {
                        show('Inject error: ' + chrome.runtime.lastError.message, true);
                        return;
                    }
                    setTimeout(() => {
                        syncGridSettingsWithTab();
                    }, 100);
                    send('sar-select-area');
                });
            });
        };
    }

    // Quality selector handler
    if (selQuality) {
        selQuality.onchange = () => {
            quality = selQuality.value;
            save();
        };
    }

    // Initialize
    load();
})();