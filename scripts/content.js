;(function(){
  if(window.__sarInjected_SARPRO) return;
  window.__sarInjected_SARPRO = true;
  
  // === CORE VARIABLES ===
  let overlay, rect, area = null, toolbar, recorder, chunks = [], timerId, startTime = 0;
  let settings = {w:1920, h:1080, br:5000000}, fmt = 'webm';
  let originalCoords = null, calibrationMode = false;
  let autoscrollSettings = { enabled: false, speed: 100, delay: 100, direction: 'down' };
  let videoStream = null, videoElement = null, canvas = null, ctx = null, animationFrameId = null;
  let scaleX = 1, scaleY = 1, offsetY = 0, sx = null, sy = null;
  let gridOverlay = null, gridStep = 10, gridEnabled = false, toolbarShadow = null;
  let autoscrollTarget = null, lastScrollTime = 0, contentObserver = null;
  let frameCount = 0, recordingFrame = null, frameCheckInterval = null;
  let frameSettings = { enabled: true, hideOnRecord: false };
  let dynamicControl = null, scrollTracker = null, controlVisible = false;
  let fullPageSelectionListener = null, scrollAnimationId = null;
  let cursorHideOverlay = null;
  let areaEditor = null, isEditingArea = false, dragStart = null, resizeHandle = null;
  let areaBackup = null, isRecording = false;
  
  // === ACCESSIBILITY VARIABLES ===
  let accessibilitySettings = { 
    enabled: false, 
    highContrast: false, 
    enhancedBorders: false,
    audioFeedback: false,
    announcements: false,
    fontSize: 'normal'
  };
  let scrollEndCounter = 0, lastScrollPos = 0;
  
  const CONTROL_HEIGHT = 40, CONTROL_OFFSET = 2, MIN_AREA_SIZE = 20, HANDLE_SIZE = 12;
  const KEY_NUDGE = 1, KEY_NUDGE_FAST = 10;
  const SAR_NAMESPACE = 'sar_pro_' + Math.random().toString(36).substr(2, 9);

  // === AUDIO VARIABLES ===
  let audioSettings = { enabled: false, source: 'microphone', micVolume: 100, systemVolume: 100 };
  let audioStream = null, micStream = null, systemAudioStream = null;
  let audioContext = null, micGainNode = null, systemGainNode = null;

  // === ELEMENT CAPTURE API SUPPORT ===
  let supportsElementCapture = false, legacyModeWarningShown = false;

  // === ACCESSIBILITY SOUND AND ANNOUNCEMENT FUNCTIONS ===
  function announceToScreenReader(message) {
    if (!accessibilitySettings.enabled || !accessibilitySettings.announcements) return;
    
    // Create a live region for screen reader announcements
    let announcer = document.getElementById('sar-announcer');
    if (!announcer) {
      announcer = document.createElement('div');
      announcer.id = 'sar-announcer';
      announcer.setAttribute('aria-live', 'polite');
      announcer.setAttribute('aria-atomic', 'true');
      announcer.style.cssText = `
        position: absolute !important;
        left: -10000px !important;
        width: 1px !important;
        height: 1px !important;
        overflow: hidden !important;
      `;
      document.body.appendChild(announcer);
    }
    
    // Clear and set new message
    announcer.textContent = '';
    setTimeout(() => {
      announcer.textContent = message;
    }, 100);
  }

  function playAccessibilitySound(type = 'notification') {
    if (!accessibilitySettings.enabled || !accessibilitySettings.audioFeedback) return;
    
    try {
      // Simple web audio API sounds
      const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
      const oscillator = audioCtx.createOscillator();
      const gainNode = audioCtx.createGain();
      
      oscillator.connect(gainNode);
      gainNode.connect(audioCtx.destination);
      
      // Different frequencies for different types
      const frequencies = {
        'notification': 800,
        'success': 1000,
        'error': 400,
        'start': 600,
        'stop': 500
      };
      
      oscillator.frequency.setValueAtTime(frequencies[type] || 800, audioCtx.currentTime);
      oscillator.type = 'sine';
      
      gainNode.gain.setValueAtTime(0.1, audioCtx.currentTime);
      gainNode.gain.exponentialRampToValueAtTime(0.01, audioCtx.currentTime + 0.1);
      
      oscillator.start(audioCtx.currentTime);
      oscillator.stop(audioCtx.currentTime + 0.1);
    } catch (err) {
      // Silently fail if audio context is not available
      console.debug('[SAR] Audio feedback not available:', err);
    }
  }

  function createAccessibleButton(text, onclick, className = '', ariaLabel = '') {
    const button = document.createElement('button');
    button.textContent = text;
    button.className = className;
    button.onclick = onclick;
    button.setAttribute('role', 'button');
    button.setAttribute('tabindex', '0');
    button.setAttribute('aria-label', ariaLabel || text);
    
    // Keyboard support
    button.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        onclick(e);
      }
    });
    
    return button;
  }

  function checkElementCaptureSupport() {
    try {
      supportsElementCapture = (
        'RestrictionTarget' in window && 
        'fromElement' in RestrictionTarget &&
        HTMLVideoElement.prototype.restrictTo !== undefined
      );
    } catch (e) {
      supportsElementCapture = false;
    }
    return supportsElementCapture;
  }

  function showLegacyModeWarning() {
    if (legacyModeWarningShown || isRecording) return;
    legacyModeWarningShown = true;
  }

  // === ACCESSIBILITY FUNCTIONS ===
  function getAccessibilityStyles() {
    if (!accessibilitySettings.enabled) return '';
    return `
      .sar-accessibility-enhanced * {
        font-size: 16px !important;
        min-height: 44px !important;
        border-width: 3px !important;
        border-color: #FFD700 !important;
      }
      .sar-accessibility-enhanced button:focus {
        outline: 3px solid #FFD700 !important;
        outline-offset: 2px !important;
      }
    `;
  }

  function applyAccessibilityStyles() {
    const existingStyle = document.getElementById('sar-accessibility-styles');
    if (existingStyle) existingStyle.remove();
    
    if (!accessibilitySettings.enabled) return;
    
    const style = document.createElement('style');
    style.id = 'sar-accessibility-styles';
    style.textContent = getAccessibilityStyles();
    document.head.appendChild(style);
    
    if (toolbar) toolbar.classList.add('sar-accessibility-enhanced');
    if (dynamicControl) dynamicControl.classList.add('sar-accessibility-enhanced');
  }

  // === UTILITY FUNCTIONS ===
  function applyGridSnapXY(x, y) {
    if(!gridEnabled) return [x, y];
    const step = gridStep || 10;
    return [Math.round(x/step)*step, Math.round(y/step)*step];
  }

  function applyGridSnapSize(w, h) {
    if(!gridEnabled) return [w, h];
    const step = gridStep || 10;
    return [Math.max(MIN_AREA_SIZE, Math.round(w/step)*step),
            Math.max(MIN_AREA_SIZE, Math.round(h/step)*step)];
  }

  function snapToGrid(x, y) {
    if (!gridEnabled) return { x, y };
    return { x: Math.round(x / gridStep) * gridStep, y: Math.round(y / gridStep) * gridStep };
  }

  function snapRectToGrid(startX, startY, endX, endY) {
    if (!gridEnabled) return { x: startX, y: startY, width: Math.abs(endX - startX), height: Math.abs(endY - startY) };
    
    const snappedStart = snapToGrid(startX, startY);
    const snappedEnd = snapToGrid(endX, endY);
    
    return {
      x: Math.min(snappedStart.x, snappedEnd.x),
      y: Math.min(snappedStart.y, snappedEnd.y),
      width: Math.abs(snappedEnd.x - snappedStart.x),
      height: Math.abs(snappedEnd.y - snappedStart.y)
    };
  }

  // === SETTINGS FUNCTIONS ===
  function saveSettings(type, data) {
    const key = `sarPro${type}`;
    if (chrome?.storage?.sync) {
      try {
        chrome.storage.sync.set({[key]: data});
      } catch (e) {
        console.debug('[SAR] Chrome storage not available:', e);
      }
    }
    try { 
      localStorage.setItem(key, JSON.stringify(data)); 
    } catch (e) {
      console.debug('[SAR] localStorage not available:', e);
    }
  }

  async function loadSettings(type, defaultData) {
    const key = `sarPro${type}`;
    return new Promise(resolve => {
      if (chrome?.storage?.sync) {
        try {
          chrome.storage.sync.get([key], res => {
            if (res?.[key]) {
              Object.assign(defaultData, res[key]);
            } else {
              try {
                const saved = localStorage.getItem(key);
                if (saved) Object.assign(defaultData, JSON.parse(saved));
              } catch (e) {
                console.debug('[SAR] localStorage read error:', e);
              }
            }
            resolve();
          });
        } catch (e) {
          console.debug('[SAR] Chrome storage read error:', e);
          try {
            const saved = localStorage.getItem(key);
            if (saved) Object.assign(defaultData, JSON.parse(saved));
          } catch (e2) {
            console.debug('[SAR] localStorage fallback error:', e2);
          }
          resolve();
        }
      } else {
        try {
          const saved = localStorage.getItem(key);
          if (saved) Object.assign(defaultData, JSON.parse(saved));
        } catch (e) {
          console.debug('[SAR] localStorage error:', e);
        }
        resolve();
      }
    });
  }

  // === CURSOR HIDE OVERLAY ===
  function createCursorHideOverlay() {
    if (!area || cursorHideOverlay) return;
    
    cursorHideOverlay = document.createElement('div');
    cursorHideOverlay.id = SAR_NAMESPACE + '-cursor-hide';
    cursorHideOverlay.style.cssText = `
      position:fixed!important;left:${area.viewportX}px!important;top:${area.viewportY}px!important;
      width:${area.width}px!important;height:${area.height}px!important;
      cursor:none!important;pointer-events:none!important;z-index:2147483641!important;
      background:transparent!important;
    `;
    document.body.appendChild(cursorHideOverlay);
  }

  function removeCursorHideOverlay() {
    if (cursorHideOverlay?.parentNode) {
      cursorHideOverlay.parentNode.removeChild(cursorHideOverlay);
      cursorHideOverlay = null;
    }
  }

  function updateCursorHideOverlay() {
    if (!cursorHideOverlay || !area) return;
    updateAreaViewportPosition();
    cursorHideOverlay.style.left = area.viewportX + 'px';
    cursorHideOverlay.style.top = area.viewportY + 'px';
    cursorHideOverlay.style.width = area.width + 'px';
    cursorHideOverlay.style.height = area.height + 'px';
  }

  // === AUDIO FUNCTIONS ===
  async function setupAudioStream() {
    if (!audioSettings.enabled) return null;
    try {
      let streams = [];
      
      if (audioSettings.source === 'microphone' || audioSettings.source === 'both') {
        try {
          micStream = await navigator.mediaDevices.getUserMedia({
            audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true, sampleRate: 44100 }
          });
          streams.push(micStream);
        } catch (err) {
          console.error('[SAR] Microphone access denied:', err);
        }
      }

      if (audioSettings.source === 'system' || audioSettings.source === 'both') {
        try {
          systemAudioStream = await navigator.mediaDevices.getDisplayMedia({
            video: false,
            audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false }
          });
          streams.push(systemAudioStream);
        } catch (err) {
          console.error('[SAR] System audio access denied:', err);
        }
      }

      if (streams.length === 0) return null;
      if (streams.length === 1) return audioStream = streams[0];
      return await mixAudioStreams(streams);
    } catch (err) {
      console.error('[SAR] Audio setup failed:', err);
      return null;
    }
  }

  async function mixAudioStreams(streams) {
    try {
      audioContext = new (window.AudioContext || window.webkitAudioContext)();
      const destination = audioContext.createMediaStreamDestination();

      for (let stream of streams) {
        const source = audioContext.createMediaStreamSource(stream);
        const gainNode = audioContext.createGain();
        
        if (stream === micStream) {
          gainNode.gain.value = audioSettings.micVolume / 100;
          micGainNode = gainNode;
        } else if (stream === systemAudioStream) {
          gainNode.gain.value = audioSettings.systemVolume / 100;
          systemGainNode = gainNode;
        }

        source.connect(gainNode);
        gainNode.connect(destination);
      }

      return audioStream = destination.stream;
    } catch (err) {
      console.error('[SAR] Audio mixing failed:', err);
      return streams[0];
    }
  }

  function updateAudioVolume() {
    if (micGainNode) micGainNode.gain.value = audioSettings.micVolume / 100;
    if (systemGainNode) systemGainNode.gain.value = audioSettings.systemVolume / 100;
  }

  function stopAudioStreams() {
    [micStream, systemAudioStream].forEach(stream => {
      if (stream) stream.getTracks().forEach(track => track.stop());
    });
    if (audioContext) audioContext.close();
    micStream = systemAudioStream = audioStream = audioContext = micGainNode = systemGainNode = null;
  }

  // === AREA EDITOR FUNCTIONS ===
  function createAreaEditor() {
    if (areaEditor || !area) return;
    
    areaBackup = JSON.parse(JSON.stringify(area));
    
    areaEditor = document.createElement('div');
    areaEditor.setAttribute('role', 'dialog');
    areaEditor.setAttribute('aria-label', 'Edit recording area');
    
    const borderStyle = accessibilitySettings.enabled && accessibilitySettings.enhancedBorders ? 
      'border:4px solid #FFD700!important;' : 'border:2px solid #4CAF50!important;';
    
    areaEditor.style.cssText = `
      position:fixed!important;left:${area.viewportX}px!important;top:${area.viewportY}px!important;
      width:${area.width}px!important;height:${area.height}px!important;
      ${borderStyle}background:rgba(76,175,80,0.1)!important;
      cursor:move!important;z-index:2147483646!important;pointer-events:auto!important;
      box-shadow:0 0 0 2px rgba(0,0,0,0.5),inset 0 0 0 1px rgba(255,255,255,0.3)!important;
    `;
    
    if (accessibilitySettings.enabled) {
      areaEditor.classList.add('sar-accessibility-enhanced');
    }
    
    // Add resize handles
    ['nw', 'ne', 'sw', 'se', 'n', 's', 'e', 'w'].forEach(handle => {
      const handleEl = document.createElement('div');
      handleEl.className = `resize-handle-${handle}`;
      handleEl.setAttribute('role', 'button');
      handleEl.setAttribute('aria-label', `Resize ${handle}`);
      handleEl.setAttribute('tabindex', '0');
      
      const handleSize = accessibilitySettings.enabled ? '16px' : '12px';
      let pos = '';
      switch(handle) {
        case 'nw': pos = `top:-8px!important;left:-8px!important;width:${handleSize}!important;height:${handleSize}!important;cursor:nw-resize!important;border-radius:2px!important;`; break;
        case 'ne': pos = `top:-8px!important;right:-8px!important;width:${handleSize}!important;height:${handleSize}!important;cursor:ne-resize!important;border-radius:2px!important;`; break;
        case 'sw': pos = `bottom:-8px!important;left:-8px!important;width:${handleSize}!important;height:${handleSize}!important;cursor:sw-resize!important;border-radius:2px!important;`; break;
        case 'se': pos = `bottom:-8px!important;right:-8px!important;width:${handleSize}!important;height:${handleSize}!important;cursor:se-resize!important;border-radius:2px!important;`; break;
        case 'n': pos = `top:-6px!important;left:50%!important;transform:translateX(-50%)!important;width:24px!important;height:12px!important;cursor:n-resize!important;border-radius:6px!important;`; break;
        case 's': pos = `bottom:-6px!important;left:50%!important;transform:translateX(-50%)!important;width:24px!important;height:12px!important;cursor:s-resize!important;border-radius:6px!important;`; break;
        case 'e': pos = `right:-6px!important;top:50%!important;transform:translateY(-50%)!important;width:12px!important;height:24px!important;cursor:e-resize!important;border-radius:6px!important;`; break;
        case 'w': pos = `left:-6px!important;top:50%!important;transform:translateY(-50%)!important;width:12px!important;height:24px!important;cursor:w-resize!important;border-radius:6px!important;`; break;
      }
      
      const handleColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : '#4CAF50';
      handleEl.style.cssText = `position:absolute!important;background:${handleColor}!important;border:2px solid #fff!important;z-index:2147483647!important;pointer-events:auto!important;${pos}`;
      
      handleEl.addEventListener('mousedown', (e) => startResize(e, handle));
      handleEl.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          startResize(e, handle);
        }
      });
      
      areaEditor.appendChild(handleEl);
    });
    
    // Add area info display
    const infoBox = document.createElement('div');
    const fontSize = accessibilitySettings.enabled ? '14px' : '11px';
    const bgColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? 
      'rgba(0,0,0,0.95)' : 'rgba(76,175,80,0.9)';
    
    infoBox.style.cssText = `
      position:absolute!important;top:-35px!important;left:0!important;
      background:${bgColor}!important;color:white!important;padding:6px 10px!important;
      border-radius:4px!important;font-family:Arial,sans-serif!important;font-size:${fontSize}!important;
      font-weight:bold!important;pointer-events:none!important;white-space:nowrap!important;
      text-shadow: 1px 1px 2px rgba(0,0,0,0.8)!important;
    `;
    infoBox.textContent = `${area.width}×${area.height}`;
    areaEditor.appendChild(infoBox);
    
    // Add Save/Cancel buttons
    const bar = document.createElement('div');
    bar.className = 'sar-area-edit-bar';
    bar.style.cssText = 'position:absolute!important;top:-40px!important;right:0!important;display:flex!important;gap:6px!important;z-index:2147483647!important;';
    
    const btnSave = createAccessibleButton('Save', saveAreaEditing, '', 'Save area changes (Enter)');
    const btnCancel = createAccessibleButton('Cancel', cancelAreaEditing, '', 'Cancel area editing (Escape)');
    
    const buttonStyle = accessibilitySettings.enabled ? 
      'padding:8px 12px!important;font-size:14px!important;min-height:36px!important;' : 
      'padding:4px 8px!important;font-size:12px!important;';
    
    btnSave.style.cssText = `${buttonStyle}background:#4CAF50!important;color:white!important;border:2px solid #fff!important;border-radius:6px!important;cursor:pointer!important;font-weight:bold!important;`;
    btnCancel.style.cssText = `${buttonStyle}background:#f44336!important;color:white!important;border:2px solid #fff!important;border-radius:6px!important;cursor:pointer!important;font-weight:bold!important;`;
    
    bar.append(btnSave, btnCancel);
    areaEditor.appendChild(bar);
    
    // Add drag functionality
    areaEditor.addEventListener('mousedown', startDrag);
    areaEditor.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        startDrag(e);
      }
    });
    
    document.body.appendChild(areaEditor);
    isEditingArea = true;
    
    announceToScreenReader('Area editor opened');
    playAccessibilitySound('notification');
  }

  // [The rest of the functions remain the same but I'll continue with the key fixes...]

  // Replace showNotification function with error handling
  function showNotification(message, type = 'info') {
    if (isRecording) return;
    
    try {
      const notification = document.createElement('div');
      notification.setAttribute('role', 'alert');
      notification.setAttribute('aria-live', 'assertive');
      
      const bgColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ?
        (type === 'error' ? '#AA0000' : type === 'success' ? '#006600' : '#0066CC') :
        (type === 'error' ? '#f44336' : type === 'success' ? '#4CAF50' : '#2196F3');
        
      const fontSize = accessibilitySettings.enabled ? '14px' : '11px';
      const padding = accessibilitySettings.enabled ? '12px 16px' : '8px 12px';
      const maxWidth = accessibilitySettings.enabled ? '350px' : '250px';
      
      notification.style.cssText = `
        position:fixed!important;top:80px!important;right:20px!important;z-index:2147483646!important;
        background:${bgColor}!important;
        color:white!important;padding:${padding}!important;border-radius:8px!important;
        font-family:Arial,sans-serif!important;font-size:${fontSize}!important;font-weight:bold!important;
        box-shadow:0 4px 12px rgba(0,0,0,0.4)!important;max-width:${maxWidth}!important;
        word-wrap:break-word!important;animation:slideIn 0.3s ease-out!important;pointer-events:auto!important;
        border: 2px solid rgba(255,255,255,0.3)!important;
        text-shadow: 1px 1px 2px rgba(0,0,0,0.8)!important;
      `;
      
      if (accessibilitySettings.enabled) {
        notification.classList.add('sar-accessibility-enhanced');
      }
      
      notification.innerHTML = `
        <div style="display:flex;align-items:center;gap:8px;">
          <span style="font-size:${accessibilitySettings.enabled ? '16px' : '14px'};">${type === 'error' ? '❌' : type === 'success' ? '✅' : 'ℹ️'}</span>
          <span>${message}</span>
        </div>
      `;
      
      if (!document.getElementById('sar-notification-styles')) {
        const style = document.createElement('style');
        style.id = 'sar-notification-styles';
        style.textContent = `
          @keyframes slideIn { from { transform: translateX(100%); opacity: 0; } to { transform: translateX(0); opacity: 1; } }
          @keyframes slideOut { from { transform: translateX(0); opacity: 1; } to { transform: translateX(100%); opacity: 0; } }
        `;
        document.head.appendChild(style);
      }
      
      document.body.appendChild(notification);
      
      // Auto-announce for screen readers
      announceToScreenReader(message);
      playAccessibilitySound(type);
      
      const timeout = accessibilitySettings.enabled ? 4000 : 2000;
      setTimeout(() => {
        notification.style.animation = 'slideOut 0.3s ease-in';
        setTimeout(() => notification.parentNode?.removeChild(notification), 300);
      }, timeout);
      
      notification.onclick = () => {
        notification.style.animation = 'slideOut 0.3s ease-in';
        setTimeout(() => notification.parentNode?.removeChild(notification), 300);
      };
      
      notification.setAttribute('tabindex', '0');
      notification.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ' || e.key === 'Escape') {
          e.preventDefault();
          notification.click();
        }
      });
    } catch (err) {
      console.error('[SAR] Notification error:', err);
    }
  }

  // === INITIALIZATION ===
  Promise.all([
    loadSettings('Grid', {enabled: gridEnabled, step: gridStep}),
    loadSettings('Autoscroll', autoscrollSettings),
    loadSettings('Frame', frameSettings),
    loadSettings('Format', {format: fmt, quality: settings}),
    loadSettings('Audio', audioSettings),
    loadSettings('Accessibility', accessibilitySettings)
  ]).then(() => {
    setupHotkeys();
    checkElementCaptureSupport();
    applyAccessibilityStyles();
    
    console.log(`✅ SAR Pro initialized with full accessibility support!`);
    console.log(`🎥 Format: ${fmt.toUpperCase()}`);
    console.log(`🎤 Audio: ${audioSettings.enabled ? `${audioSettings.source} (${audioSettings.micVolume}%/${audioSettings.systemVolume}%)` : 'disabled'}`);
    console.log(`♿ Accessibility: ${accessibilitySettings.enabled ? 'enabled' : 'disabled'}`);
    
    if (supportsElementCapture) {
      console.log(`🚀 Element Capture API: SUPPORTED (Chrome 132+)`);
    } else {
      console.log(`⚠️ Element Capture API: NOT SUPPORTED - Using Legacy Mode`);
    }
    
    // Initial notification with accessibility considerations
    const modeText = supportsElementCapture ? 
      '🚀 Element Capture Ready!' : 
      '⚠️ Legacy Mode. Use H key to hide panel.';
      
    const audioText = audioSettings.enabled ? 
      ` Audio: ${audioSettings.source}` : '';
      
    const accessibilityText = accessibilitySettings.enabled ?
      ' ♿ Accessibility enabled' : '';
      
    const message = `🎯 Screen Area Recorder Ready!${audioText}${accessibilityText}\n• Select area or Ctrl+A for full page\n• Click ♿ button for accessibility mode\n${modeText}`;
    
    showNotification(message, 'info');
    
    // Announce initialization for screen readers
    setTimeout(() => {
      announceToScreenReader('Screen Area Recorder Pro initialized and ready. Use the accessibility button in the control panel for enhanced features, or press H for help.');
    }, 1000);
    
  }).catch(error => {
    console.error('[SAR] Initialization error:', error);
    try {
      showNotification('Initialization error - some features may not work properly', 'error');
    } catch (e) {
      console.error('[SAR] Failed to show error notification:', e);
    }
  });

  function startDrag(e) {
    if (e.target.className.includes('resize-handle') || e.target.tagName === 'BUTTON') return;
    
    e.preventDefault();
    dragStart = {
      x: e.clientX - parseInt(areaEditor.style.left),
      y: e.clientY - parseInt(areaEditor.style.top)
    };
    
    document.addEventListener('mousemove', onDrag);
    document.addEventListener('mouseup', stopDrag);
  }

  function onDrag(e) {
    if (!dragStart) return;
    
    let newX = e.clientX - dragStart.x;
    let newY = e.clientY - dragStart.y;
    [newX, newY] = applyGridSnapXY(newX, newY);

    newX = Math.max(0, Math.min(newX, window.innerWidth - area.width));
    newY = Math.max(0, Math.min(newY, window.innerHeight - area.height));
    
    areaEditor.style.left = newX + 'px';
    areaEditor.style.top = newY + 'px';
    
    area.viewportX = newX;
    area.viewportY = newY;
    area.x = newX + window.scrollX;
    area.y = newY + window.scrollY;
    
    updateAreaInfo();
    syncEditingUI();
  }

  function stopDrag() {
    dragStart = null;
    document.removeEventListener('mousemove', onDrag);
    document.removeEventListener('mouseup', stopDrag);
  }

  function startResize(e, handle) {
    e.preventDefault();
    e.stopPropagation();
    
    resizeHandle = handle;
    const startX = e.clientX, startY = e.clientY;
    const startRect = {
      x: parseInt(areaEditor.style.left), y: parseInt(areaEditor.style.top),
      width: parseInt(areaEditor.style.width), height: parseInt(areaEditor.style.height)
    };
    
    function onResize(ev) {
      const deltaX = ev.clientX - startX, deltaY = ev.clientY - startY;
      
      let newX = startRect.x, newY = startRect.y, newWidth = startRect.width, newHeight = startRect.height;
      
      switch(resizeHandle) {
        case 'nw': newX = startRect.x + deltaX; newY = startRect.y + deltaY; newWidth = startRect.width - (newX - startRect.x); newHeight = startRect.height - (newY - startRect.y); break;
        case 'ne': newY = startRect.y + deltaY; newWidth = startRect.width + deltaX; newHeight = startRect.height - (newY - startRect.y); break;
        case 'sw': newX = startRect.x + deltaX; newWidth = startRect.width - (newX - startRect.x); newHeight = startRect.height + deltaY; break;
        case 'se': newWidth = startRect.width + deltaX; newHeight = startRect.height + deltaY; break;
        case 'n': newY = startRect.y + deltaY; newHeight = startRect.height - (newY - startRect.y); break;
        case 's': newHeight = startRect.height + deltaY; break;
        case 'e': newWidth = startRect.width + deltaX; break;
        case 'w': newX = startRect.x + deltaX; newWidth = startRect.width - (newX - startRect.x); break;
      }
      
      [newX, newY] = applyGridSnapXY(newX, newY);
      [newWidth, newHeight] = applyGridSnapSize(newWidth, newHeight);

      if (newX < 0) { newWidth += newX; newX = 0; }
      if (newY < 0) { newHeight += newY; newY = 0; }
      if (newX + newWidth > window.innerWidth) newWidth = window.innerWidth - newX;
      if (newY + newHeight > window.innerHeight) newHeight = window.innerHeight - newY;
      
      areaEditor.style.left = newX + 'px';
      areaEditor.style.top = newY + 'px';
      areaEditor.style.width = newWidth + 'px';
      areaEditor.style.height = newHeight + 'px';
      
      area.viewportX = newX; area.viewportY = newY;
      area.width = newWidth; area.height = newHeight;
      area.x = newX + window.scrollX; area.y = newY + window.scrollY;
      
      updateAreaInfo();
      syncEditingUI();
    }
    
    function stopResize() {
      resizeHandle = null;
      document.removeEventListener('mousemove', onResize);
      document.removeEventListener('mouseup', stopResize);
    }
    
    document.addEventListener('mousemove', onResize);
    document.addEventListener('mouseup', stopResize);
  }

  function updateAreaInfo() {
    if (!areaEditor) return;
    const infoBox = areaEditor.querySelector('div[style*="top: -35px"], div[style*="top:-35px"]');
    if (infoBox) infoBox.textContent = `${area.width}×${area.height}`;
  }

  function syncEditingUI() {
    if (recordingFrame && recordingFrame.style.display !== 'none') {
      recordingFrame.style.left = areaEditor.style.left;
      recordingFrame.style.top = areaEditor.style.top;
      recordingFrame.style.width = areaEditor.style.width;
      recordingFrame.style.height = areaEditor.style.height;
    }
    if (dynamicControl && controlVisible) positionDynamicControl();

    const r = areaEditor.getBoundingClientRect();
    if (r.top < 0) {
      window.scrollBy({ top: r.top - 50, behavior: 'smooth' });
    } else if (r.bottom > window.innerHeight) {
      window.scrollBy({ top: r.bottom - window.innerHeight + 50, behavior: 'smooth' });
    }
  }

  function saveAreaEditing() {
    removeAreaEditor();
    originalCoords = { x: area.x, y: area.y, viewportX: area.viewportX, viewportY: area.viewportY };
    showDynamicControl();
    if (frameSettings.enabled && !recorder?.state) createRecordingFrame();
    if (toolbar) toolbar.style.display = 'block';
    
    const cx = area.viewportX + area.width / 2;
    const cy = area.viewportY + area.height / 2;
    const el = document.elementFromPoint(cx, cy);
    let scrollable = null;

    if (el) scrollable = findScrollableParent(el);

    if (!scrollable && document.scrollingElement && 
        document.documentElement.scrollHeight > window.innerHeight) {
      scrollable = document.scrollingElement;
    }

    if (scrollable) {
      autoscrollTarget = scrollable;
      window.__sarAutoscrollTarget = scrollable;
      setupContentObserver(scrollable);
      
      if (!autoscrollSettings.enabled && scrollable !== document.scrollingElement) {
        autoscrollSettings.enabled = true;
        const asEnable = toolbarShadow?.querySelector('#as-enable');
        if (asEnable) asEnable.checked = true;
      }
    }
    
    saveSettings('LastArea', area);
    areaBackup = null;
    isEditingArea = false;
    
    announceToScreenReader('Area saved');
    playAccessibilitySound('success');
  }

  function cancelAreaEditing() {
    if (areaBackup) {
      area = JSON.parse(JSON.stringify(areaBackup));
      if (recordingFrame) {
        recordingFrame.style.left = area.viewportX + 'px';
        recordingFrame.style.top = area.viewportY + 'px';
        recordingFrame.style.width = area.width + 'px';
        recordingFrame.style.height = area.height + 'px';
      }
      
      const cx = area.viewportX + area.width / 2;
      const cy = area.viewportY + area.height / 2;
      const el = document.elementFromPoint(cx, cy);
      let scrollable = null;

      if (el) scrollable = findScrollableParent(el);

      if (!scrollable && document.scrollingElement && 
          document.documentElement.scrollHeight > window.innerHeight) {
        scrollable = document.scrollingElement;
      }

      if (scrollable) {
        autoscrollTarget = scrollable;
        window.__sarAutoscrollTarget = scrollable;
        setupContentObserver(scrollable);
      }
    }
    removeAreaEditor();
    showDynamicControl();
    if (toolbar) toolbar.style.display = 'block';
    areaBackup = null;
    isEditingArea = false;
    
    announceToScreenReader('Area editing cancelled');
    playAccessibilitySound('notification');
  }

  function removeAreaEditor() {
    if (areaEditor?.parentNode) {
      areaEditor.parentNode.removeChild(areaEditor);
      areaEditor = null;
      isEditingArea = false;
    }
  }

  // === DYNAMIC CONTROL FUNCTIONS ===
  function createDynamicControl() {
    if (dynamicControl) return;
    
    dynamicControl = document.createElement('div');
    dynamicControl.id = SAR_NAMESPACE + '-dynamic-control';
    dynamicControl.setAttribute('role', 'toolbar');
    dynamicControl.setAttribute('aria-label', 'Recording controls');
    
    const controlHeight = accessibilitySettings.enabled ? '50px' : '40px';
    const minWidth = accessibilitySettings.enabled ? '400px' : '300px';
    const borderColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? 
      '#FFD700' : 'rgba(76,175,80,0.4)';
    
    dynamicControl.style.cssText = `
      position:fixed!important;height:${controlHeight}!important;z-index:2147483647!important;
      background:linear-gradient(135deg,rgba(0,0,0,0.95),rgba(20,20,20,0.95))!important;
      backdrop-filter:blur(15px)!important;border:2px solid ${borderColor}!important;
      border-radius:20px!important;box-shadow:0 4px 20px rgba(0,0,0,0.6)!important;
      display:none!important;align-items:center!important;justify-content:center!important;
      padding:0 15px!important;font-family:Arial,sans-serif!important;user-select:none!important;
      pointer-events:auto!important;min-width:${minWidth}!important;transition:all 0.3s ease!important;
    `;
    
    if (accessibilitySettings.enabled) {
      dynamicControl.classList.add('sar-accessibility-enhanced');
    }
    
    document.body.appendChild(dynamicControl);
  }

  function smartPosition(element, referenceArea, preferredPosition = 'above') {
    if (!element || !referenceArea) return;
    
    const elementWidth = Math.min(accessibilitySettings.enabled ? 500 : 400, referenceArea.width, window.innerWidth - 20);
    const elementHeight = element.offsetHeight || (accessibilitySettings.enabled ? 50 : CONTROL_HEIGHT);
    
    const idealX = referenceArea.viewportX + (referenceArea.width - elementWidth) / 2;
    const finalX = Math.max(10, Math.min(idealX, window.innerWidth - elementWidth - 10));
    
    const offset = accessibilitySettings.enabled ? 10 : CONTROL_OFFSET;
    const positions = [
      { name: 'above', y: referenceArea.viewportY - elementHeight - offset, valid: referenceArea.viewportY >= elementHeight + offset + 10 },
      { name: 'below', y: referenceArea.viewportY + referenceArea.height + offset, valid: referenceArea.viewportY + referenceArea.height + elementHeight + offset + 10 <= window.innerHeight },
      { name: 'floating-top', y: 10, valid: true },
      { name: 'floating-bottom', y: window.innerHeight - elementHeight - 10, valid: true }
    ];

    let bestPosition = positions.find(p => p.name === preferredPosition && p.valid) || 
                     positions.find(p => p.valid) || 
                     positions[positions.length - 1];

    if (toolbar && toolbar.style.display !== 'none') {
      const toolbarRect = toolbar.getBoundingClientRect();
      if (bestPosition.y < toolbarRect.bottom + 10 && bestPosition.y + elementHeight > toolbarRect.top - 10) {
        bestPosition = positions.find(p => p.y > toolbarRect.bottom + 10) || bestPosition;
      }
    }

    element.style.left = `${finalX}px`;
    element.style.top = `${bestPosition.y}px`;
    element.style.width = `${elementWidth}px`;
    element.style.display = 'flex';
    
    const colors = {
      'above': accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : 'rgba(76,175,80,0.4)',
      'below': accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : 'rgba(255,152,0,0.6)', 
      'floating-top': accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : 'rgba(33,150,243,0.6)',
      'floating-bottom': accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : 'rgba(33,150,243,0.6)'
    };
    element.style.borderColor = colors[bestPosition.name] || colors['above'];
    
    return bestPosition.name;
  }

  function positionDynamicControl() {
    if (!dynamicControl || !area) return;
    updateAreaViewportPosition();
    
    const areaVisible = area.viewportY < window.innerHeight && 
                       (area.viewportY + area.height) > 0 &&
                       area.viewportX < window.innerWidth &&
                       (area.viewportX + area.width) > 0;
    
    if (areaVisible) {
      smartPosition(dynamicControl, area, 'above');
      controlVisible = true;
    } else {
      dynamicControl.style.display = 'none';
      controlVisible = false;
    }
  }

  function updateAreaViewportPosition() {
    if (!area || !originalCoords) return;
    area.viewportX = originalCoords.x - window.scrollX;
    area.viewportY = originalCoords.y - window.scrollY;
  }

  function showDynamicControl() {
    if (!dynamicControl) createDynamicControl();
    updateDynamicControlContent(false, false);
    positionDynamicControl();
    startScrollTracking();
  }

  function hideDynamicControl() {
    if (dynamicControl) {
      dynamicControl.style.display = 'none';
      controlVisible = false;
    }
    stopScrollTracking();
  }

  function removeDynamicControl() {
    stopScrollTracking();
    if (dynamicControl?.parentNode) {
      dynamicControl.parentNode.removeChild(dynamicControl);
      dynamicControl = null;
      controlVisible = false;
    }
  }

  function updateDynamicControlContent(isRecording = false, isPaused = false) {
    if (!dynamicControl) return;
    
    const status = isPaused ? 'PAUSED' : (isRecording ? 'RECORDING' : 'READY');
    const statusColor = isPaused ? '#ff9800' : (isRecording ? '#f44336' : '#4CAF50');
    const statusIcon = isPaused ? '⏸' : (isRecording ? '🔴' : '🎯');
    const audioIndicator = audioSettings.enabled ? 
      (audioSettings.source === 'microphone' ? '🎤' : 
       audioSettings.source === 'system' ? '🔊' : '🎤🔊') : '';
    
    // Адаптивные размеры для accessibility
    const isAccessible = accessibilitySettings.enabled;
    const fontSize = isAccessible ? '13px' : '11px';
    const buttonPadding = isAccessible ? '6px 10px' : '5px 10px';
    const buttonFontSize = isAccessible ? '11px' : '10px';
    const buttonMinHeight = isAccessible ? '32px' : '28px';
    const containerMinWidth = isAccessible ? '500px' : '320px';
    const containerHeight = isAccessible ? '50px' : '40px';
    const accessibilityIcon = isAccessible ? '♿' : '👁';
    const accessibilityTitle = isAccessible ? 'Disable Accessibility' : 'Enable Accessibility';
    const accessibilityBg = isAccessible ? '#FFD700' : 'rgba(255,255,255,0.15)';
    const accessibilityColor = isAccessible ? '#000' : '#fff';
    
    // Обновляем размеры контейнера
    dynamicControl.style.minWidth = containerMinWidth;
    dynamicControl.style.height = containerHeight;
    
    dynamicControl.innerHTML = `
      <div style="display:flex;align-items:center;gap:${isAccessible ? '8px' : '6px'};width:100%;flex-wrap:${isAccessible ? 'wrap' : 'nowrap'};justify-content:space-between;">
        <div style="display:flex;align-items:center;gap:6px;flex-shrink:0;">
          <span style="font-size:${fontSize};">${statusIcon}</span>
          <span style="color:${statusColor};font-weight:bold;font-size:${fontSize};">${status}</span>
          ${audioIndicator ? `<span style="font-size:${fontSize};">${audioIndicator}</span>` : ''}
          ${isAccessible ? `<span style="font-size:${fontSize};color:#FFD700;" title="Accessibility Mode">♿</span>` : ''}
          ${area && !isAccessible ? `<span style="color:#fff;font-size:${fontSize};opacity:0.7;">│ ${area.width}×${area.height}</span>` : ''}
        </div>
        
        <div style="display:flex;gap:${isAccessible ? '4px' : '6px'};align-items:center;flex-wrap:${isAccessible ? 'wrap' : 'nowrap'};">
          ${!isRecording ? `
            <button id="dynamic-start-btn" aria-label="Start recording" style="padding:${buttonPadding};background:#4CAF50;color:white;border:none;border-radius:${isAccessible ? '8px' : '12px'};cursor:pointer;font-weight:600;font-size:${buttonFontSize};min-height:${buttonMinHeight};white-space:nowrap;transition:all 0.2s ease;">▶ Start</button>
            <button id="dynamic-edit-btn" aria-label="Edit area" style="padding:${buttonPadding};background:rgba(33,150,243,0.9);color:white;border:none;border-radius:${isAccessible ? '8px' : '12px'};cursor:pointer;font-weight:600;font-size:${buttonFontSize};min-height:${buttonMinHeight};white-space:nowrap;transition:all 0.2s ease;">✏ Edit</button>
            <button id="dynamic-accessibility-btn" aria-label="${accessibilityTitle}" title="${accessibilityTitle}" style="padding:${isAccessible ? '6px 8px' : buttonPadding};background:${accessibilityBg};color:${accessibilityColor};border:none;border-radius:${isAccessible ? '8px' : '12px'};cursor:pointer;font-weight:bold;font-size:${isAccessible ? '13px' : buttonFontSize};min-height:${buttonMinHeight};transition:all 0.2s ease;box-shadow:${isAccessible ? '0 2px 8px rgba(255,215,0,0.3)' : 'none'};">${accessibilityIcon}</button>
            <button id="dynamic-settings-btn" aria-label="Open settings" style="padding:${isAccessible ? '6px 8px' : buttonPadding};background:rgba(255,255,255,0.15);color:white;border:none;border-radius:${isAccessible ? '8px' : '12px'};cursor:pointer;font-weight:600;font-size:${buttonFontSize};min-height:${buttonMinHeight};transition:all 0.2s ease;">⚙</button>
          ` : `
            <button id="dynamic-pause-btn" aria-label="${isPaused ? 'Resume recording' : 'Pause recording'}" style="padding:${buttonPadding};background:${isPaused ? '#4CAF50' : '#ff9800'};color:white;border:none;border-radius:${isAccessible ? '8px' : '12px'};cursor:pointer;font-weight:600;font-size:${buttonFontSize};min-height:${buttonMinHeight};white-space:nowrap;transition:all 0.2s ease;">${isPaused ? '▶' : '⏸'}</button>
            <button id="dynamic-stop-btn" aria-label="Stop recording" style="padding:${buttonPadding};background:#f44336;color:white;border:none;border-radius:${isAccessible ? '8px' : '12px'};cursor:pointer;font-weight:600;font-size:${buttonFontSize};min-height:${buttonMinHeight};white-space:nowrap;transition:all 0.2s ease;">⏹ Stop</button>
          `}
          <button id="dynamic-close-btn" aria-label="Close recorder" style="padding:${isAccessible ? '6px 8px' : buttonPadding};background:rgba(255,255,255,0.15);color:#fff;border:none;border-radius:${isAccessible ? '8px' : '12px'};cursor:pointer;font-weight:600;font-size:${buttonFontSize};min-height:${buttonMinHeight};transition:all 0.2s ease;">✕</button>
        </div>
        
        ${isAccessible && area ? `
        <div style="width:100%;text-align:center;margin-top:4px;font-size:10px;color:rgba(255,255,255,0.8);">
          📐 ${area.width}×${area.height}px
        </div>
        ` : ''}
      </div>
    `;
    
    setupDynamicControlEvents();
  }

  function setupDynamicControlEvents() {
    const btns = {
      start: dynamicControl?.querySelector('#dynamic-start-btn'),
      stop: dynamicControl?.querySelector('#dynamic-stop-btn'),
      pause: dynamicControl?.querySelector('#dynamic-pause-btn'),
      edit: dynamicControl?.querySelector('#dynamic-edit-btn'),
      accessibility: dynamicControl?.querySelector('#dynamic-accessibility-btn'),
      settings: dynamicControl?.querySelector('#dynamic-settings-btn'),
      close: dynamicControl?.querySelector('#dynamic-close-btn')
    };
    
    Object.values(btns).forEach(btn => {
      if (btn) {
        btn.setAttribute('role', 'button');
        btn.setAttribute('tabindex', '0');
        
        btn.addEventListener('keydown', (e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            btn.click();
          }
        });
        
        if (accessibilitySettings.enabled) {
          btn.addEventListener('focus', () => {
            btn.style.outline = '3px solid #FFD700';
            btn.style.outlineOffset = '2px';
          });
          btn.addEventListener('blur', () => {
            btn.style.outline = 'none';
          });
        }
      }
    });
    
    if (btns.start) {
      btns.start.onclick = (e) => {
        e.preventDefault(); e.stopPropagation();
        if (area) {
          announceToScreenReader('Starting recording');
          playAccessibilitySound('start');
          startRec();
        }
      };
    }
    
    if (btns.stop) {
      btns.stop.onclick = (e) => { 
        e.preventDefault(); e.stopPropagation(); 
        announceToScreenReader('Stopping recording');
        playAccessibilitySound('stop');
        stopRec(); 
      };
    }
    
    if (btns.pause) {
      btns.pause.onclick = (e) => {
        e.preventDefault(); e.stopPropagation();
        if (recorder?.state === 'recording') {
          announceToScreenReader('Recording paused');
          playAccessibilitySound('notification');
          recorder.pause();
          updateDynamicControlContent(true, true);
        } else if (recorder?.state === 'paused') {
          announceToScreenReader('Recording resumed');
          playAccessibilitySound('notification');
          recorder.resume();
          updateDynamicControlContent(true, false);
        }
      };
    }
    
    if (btns.edit) {
      btns.edit.onclick = (e) => {
        e.preventDefault(); e.stopPropagation();
        if (area) {
          announceToScreenReader('Opening area editor');
          playAccessibilitySound('notification');
          hideDynamicControl();
          createAreaEditor();
        }
      };
    }
    
    if (btns.accessibility) {
      btns.accessibility.onclick = (e) => {
        e.preventDefault(); e.stopPropagation();
        
        // Переключаем accessibility режим
        accessibilitySettings.enabled = !accessibilitySettings.enabled;
        
        // Автоматически включаем все accessibility функции
        if (accessibilitySettings.enabled) {
          accessibilitySettings.highContrast = true;
          accessibilitySettings.enhancedBorders = true;
          accessibilitySettings.audioFeedback = true;
          accessibilitySettings.announcements = true;
          accessibilitySettings.fontSize = 'large';
        }
        
        saveSettings('Accessibility', accessibilitySettings);
        applyAccessibilityStyles();
        
        // Обновляем интерфейс
        updateDynamicControlContent(recorder?.state === 'recording', recorder?.state === 'paused');
        
        const status = accessibilitySettings.enabled ? 'enabled' : 'disabled';
        announceToScreenReader(`Accessibility mode ${status}`);
        playAccessibilitySound(accessibilitySettings.enabled ? 'success' : 'notification');
        
        // Показываем уведомление
        showNotification(
          accessibilitySettings.enabled ? 
            '♿ Accessibility mode enabled: Large fonts, high contrast, audio feedback' :
            'Accessibility mode disabled',
          accessibilitySettings.enabled ? 'success' : 'info'
        );
      };
    }
    
    if (btns.settings) {
      btns.settings.onclick = (e) => {
        e.preventDefault(); e.stopPropagation();
        if (area) {
          announceToScreenReader('Opening settings panel');
          playAccessibilitySound('notification');
          panel();
        }
      };
    }
    
    if (btns.close) {
      btns.close.onclick = (e) => { 
        e.preventDefault(); e.stopPropagation(); 
        announceToScreenReader('Closing recorder');
        playAccessibilitySound('notification');
        cleanup(); 
      };
    }

    Object.values(btns).forEach(btn => {
      if (btn) {
        btn.onmouseover = () => {
          btn.style.transform = 'scale(1.05)';
          if (accessibilitySettings.enabled && accessibilitySettings.highContrast) {
            btn.style.borderColor = '#FFD700';
          }
        };
        btn.onmouseout = () => {
          btn.style.transform = 'scale(1)';
          btn.style.borderColor = 'transparent';
        };
      }
    });
  }

  function startScrollTracking() {
    if (scrollTracker) return;
    scrollTracker = setInterval(positionDynamicControl, 16);
    window.addEventListener('scroll', positionDynamicControl, { passive: true });
    window.addEventListener('resize', positionDynamicControl, { passive: true });
  }

  function stopScrollTracking() {
    if (scrollTracker) {
      clearInterval(scrollTracker);
      scrollTracker = null;
    }
    window.removeEventListener('scroll', positionDynamicControl);
    window.removeEventListener('resize', positionDynamicControl);
  }

  // === RECORDING FRAME ===
  function createRecordingFrame() {
    if (!area || recordingFrame || !frameSettings.enabled) return;
    
    recordingFrame = document.createElement('div');
    recordingFrame.id = SAR_NAMESPACE + '-recording-frame';
    recordingFrame.setAttribute('role', 'presentation');
    recordingFrame.setAttribute('aria-label', 'Recording area frame');
    
    const borderColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : '#ff4444';
    const borderWidth = accessibilitySettings.enabled && accessibilitySettings.enhancedBorders ? '4px' : '3px';
    
    recordingFrame.style.cssText = `
      position:fixed!important;left:${area.viewportX}px!important;top:${area.viewportY}px!important;
      width:${area.width}px!important;height:${area.height}px!important;
      border:${borderWidth} solid ${borderColor}!important;border-radius:8px!important;
      box-shadow:0 0 0 2px rgba(0,0,0,0.8),inset 0 0 0 2px rgba(255,255,255,0.3)!important;
      pointer-events:none!important;z-index:2147483642!important;
      animation:sar-pulse 2s infinite ease-in-out!important;
    `;
    
    if (!document.getElementById('sar-recording-styles')) {
      const style = document.createElement('style');
      style.id = 'sar-recording-styles';
      const pulseColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : '#ff4444';
      style.textContent = `
        @keyframes sar-pulse {
          0%, 100% { border-color: ${pulseColor}; box-shadow: 0 0 0 2px rgba(0,0,0,0.8), inset 0 0 0 2px rgba(255,255,255,0.3), 0 0 10px rgba(255,68,68,0.5); }
          50% { border-color: ${pulseColor}; box-shadow: 0 0 0 2px rgba(0,0,0,0.8), inset 0 0 0 2px rgba(255,255,255,0.3), 0 0 20px rgba(255,68,68,0.8); }
        }
      `;
      document.head.appendChild(style);
    }
    
    if (!isRecording) {
      const indicator = document.createElement('div');
      const fontSize = accessibilitySettings.enabled ? '14px' : '12px';
      const bgColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? 
        'rgba(0,0,0,0.95)' : 'rgba(255,68,68,0.95)';
      
      indicator.style.cssText = `
        position:absolute!important;top:-40px!important;left:0!important;
        background:${bgColor}!important;color:white!important;
        padding:6px 12px!important;border-radius:15px!important;
        font-family:Arial,sans-serif!important;font-size:${fontSize}!important;
        font-weight:bold!important;box-shadow:0 2px 8px rgba(0,0,0,0.3)!important;
        z-index:2147483643!important;text-shadow: 1px 1px 2px rgba(0,0,0,0.8)!important;
      `;
      indicator.innerHTML = '🎯 AREA';
      recordingFrame.appendChild(indicator);
    }
    
    document.body.appendChild(recordingFrame);
    
    if (recorder?.state === 'recording') startFrameCheck();
  }

  function removeRecordingFrame() {
    stopFrameCheck();
    if (recordingFrame?.parentNode) {
      recordingFrame.parentNode.removeChild(recordingFrame);
      recordingFrame = null;
    }
  }

  function startFrameCheck() {
    if (frameCheckInterval) clearInterval(frameCheckInterval);
    frameCheckInterval = setInterval(() => {
      if (!recorder || recorder.state !== 'recording') {
        stopFrameCheck();
        return;
      }
      if (frameSettings.enabled && !frameSettings.hideOnRecord && area && 
          !document.getElementById(SAR_NAMESPACE + '-recording-frame')) {
        recordingFrame = null;
        createRecordingFrame();
      }
    }, 2000);
  }

  function stopFrameCheck() {
    if (frameCheckInterval) {
      clearInterval(frameCheckInterval);
      frameCheckInterval = null;
    }
  }

  // === GRID FUNCTIONS ===
  function createGridOverlay() {
    if (!gridEnabled || gridOverlay) return;
    gridOverlay = document.createElement('div');
    gridOverlay.setAttribute('role', 'presentation');
    gridOverlay.setAttribute('aria-label', 'Snap grid overlay');
    
    const gridColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? 
      'rgba(255,215,0,0.8)' : 'rgba(0,150,255,0.8)';
    
    gridOverlay.style.cssText = `
      position:fixed!important;left:0!important;top:0!important;right:0!important;bottom:0!important;
      pointer-events:none!important;z-index:2147483641!important;
      background-image:linear-gradient(to right,${gridColor} 1px,transparent 1px),linear-gradient(to bottom,${gridColor} 1px,transparent 1px)!important;
      background-size:${gridStep}px ${gridStep}px!important;
    `;
    document.body.appendChild(gridOverlay);
  }

  function removeGridOverlay() {
    if (gridOverlay?.parentNode) {
      gridOverlay.parentNode.removeChild(gridOverlay);
      gridOverlay = null;
    }
  }

  // === RECORDING FUNCTIONS ===
  function getRecordingMimeType(format) {
    const mimeTypes = {
      'webm': 'video/webm;codecs=vp9',
      'mp4': 'video/mp4;codecs=h264'
    };
    
    const mimeType = mimeTypes[format] || mimeTypes['webm'];
    if (MediaRecorder.isTypeSupported(mimeType)) return mimeType;
    
    const fallbacks = ['video/webm;codecs=vp9', 'video/webm;codecs=vp8', 'video/webm', 'video/mp4'];
    for (const fallback of fallbacks) {
      if (MediaRecorder.isTypeSupported(fallback)) return fallback;
    }
    return 'video/webm';
  }

  async function startRec() {
    if (!area || recorder?.state === 'recording') return;

    try {
      isRecording = true;
      try {
        chrome?.runtime?.sendMessage?.({ type: 'sar-recording-started' });
      } catch (e) {
        console.debug('[SAR] Chrome runtime not available:', e);
      }
      removeGridOverlay();
      checkElementCaptureSupport();
      
      announceToScreenReader('Recording started');
      playAccessibilitySound('start');
      
      if (supportsElementCapture) {
        await startElementCaptureRecording();
      } else {
        showLegacyModeWarning();
        await startLegacyRecording();
      }
    } catch (err) {
      console.error('[SAR] startRec() failed:', err);
      isRecording = false;
      announceToScreenReader('Recording failed to start');
      playAccessibilitySound('error');
      stopRec();
    }
  }

  async function startElementCaptureRecording() {
    try {
      const recordingArea = createRecordingArea();
      
      videoStream = await navigator.mediaDevices.getDisplayMedia({
        video: { cursor: 'never', displaySurface: 'browser' },
        audio: false,
        preferCurrentTab: true
      });

      const [track] = videoStream.getVideoTracks();
      const restrictionTarget = await RestrictionTarget.fromElement(recordingArea);
      await track.restrictTo(restrictionTarget);

      const audioStreamResult = await setupAudioStream();
      let finalStream = videoStream;

      if (audioStreamResult) {
        finalStream = new MediaStream([
          ...videoStream.getVideoTracks(),
          ...audioStreamResult.getAudioTracks()
        ]);
      }

      setupDirectStreamRecording(finalStream);
      
    } catch (err) {
      console.error('[SAR] Element Capture API failed:', err);
      supportsElementCapture = false;
      await startLegacyRecording();
    }
  }

  async function startLegacyRecording() {
    if (toolbar && toolbar.style.display !== 'none') toolbar.style.display = 'none';
    
    createRecordingFrame();
    frameSettings.hideOnRecord ? hideRecordingFrame() : startFrameCheck();
    updateDynamicControlContent(true, false);
    
    if (dynamicControl) dynamicControl.style.display = 'none';

    videoStream = await navigator.mediaDevices.getDisplayMedia({
      video: { cursor: 'never', displaySurface: 'browser' },
      audio: false
    });

    const audioStreamResult = await setupAudioStream();
    
    videoElement = Object.assign(document.createElement('video'), { srcObject: videoStream, muted: true });
    videoElement.style.display = 'none';
    document.body.appendChild(videoElement);
    await new Promise(r => (videoElement.onloadedmetadata = () => videoElement.play().then(r)));

    const DPR = window.devicePixelRatio || 1;
    const videoIncludesUI = Math.abs(videoElement.videoHeight - window.outerHeight * DPR) < 5;

    scaleX = videoElement.videoWidth / window.innerWidth;
    scaleY = videoIncludesUI ? videoElement.videoHeight / window.outerHeight : videoElement.videoHeight / window.innerHeight;
    offsetY = videoIncludesUI ? (window.outerHeight - window.innerHeight) : 0;

    canvas = Object.assign(document.createElement('canvas'), { width: area.width, height: area.height });
    ctx = canvas.getContext('2d');
    createCursorHideOverlay();
    frameCount = 0;
    
    const drawFrame = () => {
      const sourceX = Math.max(0, Math.round(area.viewportX * scaleX));
      const sourceY = Math.max(0, Math.round((area.viewportY + offsetY) * scaleY));
      const sourceW = Math.min(Math.round(area.width * scaleX), videoElement.videoWidth - sourceX);
      const sourceH = Math.min(Math.round(area.height * scaleY), videoElement.videoHeight - sourceY);

      ctx.clearRect(0, 0, canvas.width, canvas.height);
      if (sourceW > 0 && sourceH > 0) {
        ctx.drawImage(videoElement, sourceX, sourceY, sourceW, sourceH, 0, 0, canvas.width, canvas.height);
      }
      frameCount++;
      animationFrameId = requestAnimationFrame(drawFrame);
    };
    drawFrame();

    let canvasStream = canvas.captureStream(30);
    
    if (audioStreamResult) {
      const finalStream = new MediaStream([
        ...canvasStream.getVideoTracks(),
        ...audioStreamResult.getAudioTracks()
      ]);
      setupStreamRecording(finalStream);
    } else {
      setupStreamRecording(canvasStream);
    }
  }

  function createRecordingArea() {
    const existingArea = document.getElementById(SAR_NAMESPACE + '-recording-area');
    if (existingArea) existingArea.remove();
    
    const recordingArea = document.createElement('div');
    recordingArea.id = SAR_NAMESPACE + '-recording-area';
    recordingArea.style.cssText = `
      position:fixed!important;left:${area.viewportX}px!important;top:${area.viewportY}px!important;
      width:${area.width}px!important;height:${area.height}px!important;
      pointer-events:none!important;z-index:-1!important;opacity:0!important;isolation:isolate!important;
    `;
    
    const elementsInArea = document.elementsFromPoint(
      area.viewportX + area.width / 2, 
      area.viewportY + area.height / 2
    );
    
    if (elementsInArea.length > 0) {
      const contentClone = elementsInArea[0].cloneNode(true);
      recordingArea.appendChild(contentClone);
    }
    
    document.body.appendChild(recordingArea);
    return recordingArea;
  }

  function setupDirectStreamRecording(stream) {
    const recordingMimeType = getRecordingMimeType(fmt);
    chunks = [];
    
    recorder = new MediaRecorder(stream, {
      mimeType: recordingMimeType,
      videoBitsPerSecond: settings.br
    });
    recorder.ondataavailable = e => e.data.size && chunks.push(e.data);
    recorder.onstop = () => saveRecording(fmt);
    recorder.start();
    startTime = Date.now();

    updateToolbarButtons();
    startAutoscrollIfEnabled();
  }

  function setupStreamRecording(stream) {
    const recordingMimeType = getRecordingMimeType(fmt);
    chunks = [];
    
    recorder = new MediaRecorder(stream, {
      mimeType: recordingMimeType,
      videoBitsPerSecond: settings.br
    });
    recorder.ondataavailable = e => e.data.size && chunks.push(e.data);
    recorder.onstop = () => saveRecording(fmt);
    recorder.start();
    startTime = Date.now();

    updateToolbarButtons();
    startAutoscrollIfEnabled();
  }

  function updateToolbarButtons() {
    if (toolbarShadow) {
      const btnS = toolbarShadow.querySelector('#bS');
      const btnT = toolbarShadow.querySelector('#bT');
      const btnP = toolbarShadow.querySelector('#bP');
      if (btnS) btnS.disabled = true;
      if (btnT) btnT.disabled = false;
      if (btnP) btnP.disabled = false;
    }
  }

  function startAutoscrollIfEnabled() {
    if (autoscrollSettings.enabled) {
      startSmoothScroll();
      if (autoscrollTarget) setupContentObserver(autoscrollTarget);
    }
  }

  function stopRec() {
    if (!recorder || recorder.state !== 'recording') return;

    isRecording = false;
    try {
      chrome?.runtime?.sendMessage?.({type: 'sar-recording-stopped'});
    } catch (e) {
      console.debug('[SAR] Chrome runtime not available:', e);
    }
    document.body.style.cursor = '';
    stopFrameCheck();
    updateDynamicControlContent(false, false);

    announceToScreenReader('Recording stopped');
    playAccessibilitySound('stop');

    if (!supportsElementCapture) {
      if (toolbar) toolbar.style.display = 'block';
      if (dynamicControl) positionDynamicControl();
    }

    if (frameSettings.hideOnRecord) showRecordingFrame();
    if (gridEnabled) createGridOverlay();

    recorder.stop();
    if (animationFrameId) {
      cancelAnimationFrame(animationFrameId);
      animationFrameId = null;
    }

    if (videoStream) {
      videoStream.getTracks().forEach(track => track.stop());
      videoStream = null;
    }

    stopAudioStreams();

    if (videoElement?.parentNode) {
      videoElement.pause();
      videoElement.srcObject = null;
      videoElement.parentNode.removeChild(videoElement);
      videoElement = null;
    }

    const recordingArea = document.getElementById(SAR_NAMESPACE + '-recording-area');
    if (recordingArea) recordingArea.remove();

    if (timerId) {
      clearInterval(timerId);
      timerId = null;
    }
    
    stopSmoothScroll();
    disconnectContentObserver();

    if (toolbarShadow) {
      const btnS = toolbarShadow.querySelector('#bS');
      const btnT = toolbarShadow.querySelector('#bT'); 
      const btnP = toolbarShadow.querySelector('#bP');
      if (btnS) btnS.disabled = false;
      if (btnT) btnT.disabled = true;
      if (btnP) btnP.disabled = true;
    }
  }

  function saveRecording(format) {
    try {
      const blob = new Blob(chunks, { type: 'video/webm' });
      const url = URL.createObjectURL(blob);
      const now = new Date();
      
      const extension = format === 'mp4' ? 'mp4' : 'webm';
      const audioSuffix = audioSettings.enabled ? `_${audioSettings.source}` : '';
      const fname = `ScreenArea_${now.getFullYear()}-${(now.getMonth()+1).toString().padStart(2,'0')}-${now.getDate().toString().padStart(2,'0')}_${now.getHours().toString().padStart(2,'0')}-${now.getMinutes().toString().padStart(2,'0')}-${now.getSeconds().toString().padStart(2,'0')}${audioSuffix}.${extension}`;
      
      const a = document.createElement('a');
      a.href = url;
      a.download = fname;
      document.body.appendChild(a);
      a.click();
      setTimeout(() => {
        URL.revokeObjectURL(url);
        a.remove();
      }, 200);
      
      announceToScreenReader(`Recording saved as ${fname}`);
      playAccessibilitySound('success');
      
      try {
        chrome?.runtime?.sendMessage?.({
          type: 'sar-recording-stopped',
          format: format,
          filename: fname,
          hasAudio: audioSettings.enabled
        });
      } catch (e) {
        console.debug('[SAR] Chrome runtime not available:', e);
      }
      
    } catch (err) {
      console.error('[SAR] saveRecording() failed:', err);
      announceToScreenReader('Failed to save recording');
      playAccessibilitySound('error');
    }
  }

  function hideRecordingFrame() {
    if (recordingFrame && frameSettings.hideOnRecord) {
      recordingFrame.style.display = 'none';
    }
  }

  function showRecordingFrame() {
    if (recordingFrame) {
      recordingFrame.style.display = 'block';
    }
  }

  // === AREA SELECTION FUNCTIONS ===
  function createOverlay() {
    if (overlay && document.body.contains(overlay)) return;

    overlay = document.createElement('div');
    overlay.setAttribute('role', 'application');
    overlay.setAttribute('aria-label', 'Area selection overlay');
    
    const documentHeight = Math.max(
      document.body.scrollHeight || 0,
      document.body.offsetHeight || 0,
      document.documentElement.clientHeight || 0,
      document.documentElement.scrollHeight || 0,
      document.documentElement.offsetHeight || 0,
      window.innerHeight || 0
    );
    
    const documentWidth = Math.max(
      document.body.scrollWidth || 0,
      document.body.offsetWidth || 0,
      document.documentElement.clientWidth || 0,
      document.documentElement.scrollWidth || 0,
      document.documentElement.offsetWidth || 0,
      window.innerWidth || 0
    );

    overlay.style.cssText = `
      position:absolute!important;left:0!important;top:0!important;
      width:${documentWidth}px!important;height:${documentHeight}px!important;
      cursor:crosshair!important;z-index:2147483645!important; 
      background:rgba(255,0,0,0.2)!important;pointer-events:auto!important;
    `;
    
    const fullSelectHint = document.createElement('div');
    fullSelectHint.setAttribute('role', 'status');
    fullSelectHint.setAttribute('aria-live', 'polite');
    
    const fontSize = accessibilitySettings.enabled ? '14px' : '11px';
    const padding = accessibilitySettings.enabled ? '10px 16px' : '6px 12px';
    const bgColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ?
      'rgba(0,0,0,0.95)' : 'rgba(76,175,80,0.9)';
    
    fullSelectHint.style.cssText = `
      position:fixed!important;top:20px!important;left:50%!important;transform:translateX(-50%)!important;
      background:${bgColor}!important;color:white!important;padding:${padding}!important;
      border-radius:15px!important;font-family:Arial,sans-serif!important;font-size:${fontSize}!important;
      font-weight:bold!important;z-index:2147483646!important;box-shadow:0 4px 12px rgba(0,0,0,0.4)!important;
      border: 2px solid rgba(255,255,255,0.3)!important;
      text-shadow: 1px 1px 2px rgba(0,0,0,0.8)!important;
    `;
    fullSelectHint.innerHTML = `🎯 Select area: Drag to select or Ctrl+A for full page`;
    
    document.body.appendChild(fullSelectHint);
    document.body.appendChild(overlay);

    announceToScreenReader('Area selection mode activated. Drag to select an area or press Ctrl+A for full page.');
    playAccessibilitySound('notification');

    fullPageSelectionListener = (e) => {
      if (e.ctrlKey && e.key === 'a') {
        e.preventDefault();
        selectEntirePage();
        return;
      }
    };

    overlay.addEventListener('mousedown', (e) => {
      const frameElement = findFrameUnderCursor(e);
      if (frameElement) {
        e.preventDefault();
        e.stopPropagation();
        selectFrame(frameElement);
      } else {
        down(e);
      }
    });
    
    document.addEventListener('keydown', fullPageSelectionListener);
    
    setTimeout(() => fullSelectHint.parentNode?.removeChild(fullSelectHint), accessibilitySettings.enabled ? 5000 : 3000);
  }

  function selectEntirePage() {
    const documentHeight = Math.max(
      document.body.scrollHeight || 0,
      document.body.offsetHeight || 0,
      document.documentElement.clientHeight || 0,
      document.documentElement.scrollHeight || 0,
      document.documentElement.offsetHeight || 0
    );
    
    const documentWidth = Math.max(
      document.body.scrollWidth || 0,
      document.body.offsetWidth || 0,
      document.documentElement.clientWidth || 0,
      document.documentElement.scrollWidth || 0,
      document.documentElement.offsetWidth || 0
    );

    area = {
      viewportX: 0,
      viewportY: 0,
      width: Math.min(documentWidth, window.innerWidth),
      height: Math.min(documentHeight, window.innerHeight * 3),
      x: 0,
      y: 0
    };
    
    originalCoords = { x: area.x, y: area.y, viewportX: area.viewportX, viewportY: area.viewportY };
    autoscrollTarget = document.scrollingElement || document.documentElement;
    setupContentObserver(autoscrollTarget);

    if (overlay) {
      overlay.remove();
      overlay = null;
    }

    announceToScreenReader(`Full page selected: ${area.width} by ${area.height} pixels`);
    playAccessibilitySound('success');

    showDynamicControl();
    if (frameSettings.enabled && !recorder?.state) createRecordingFrame();
    try {
      chrome?.runtime?.sendMessage?.({type: 'sar-area-selected', area});
    } catch (e) {
      console.debug('[SAR] Chrome runtime not available:', e);
    }
  }

  function isElementScrollable(element) {
    const style = window.getComputedStyle(element);
    const hasScroll = element.scrollHeight > element.clientHeight;
    const isScrollable = ['auto', 'scroll'].includes(style.overflowY);
    return hasScroll && isScrollable;
  }

  function findScrollableParent(element) {
    if (element?.tagName === 'IFRAME' || element?.tagName === 'FRAME') return element;
    
    let current = element;
    while (current && current !== document.body) {
      if (current.tagName === 'IFRAME' || current.tagName === 'FRAME') return current;
      if (isElementScrollable(current)) return current;
      current = current.parentElement;
    }

    if (document.scrollingElement && document.documentElement.scrollHeight > window.innerHeight) {
      return document.scrollingElement;
    }
    return null;
  }

  function findFrameUnderCursor(e) {
    const element = document.elementFromPoint(e.clientX, e.clientY);
    if (!element) return null;
    
    const scrollableParent = findScrollableParent(element);
    
    if (scrollableParent === document.scrollingElement || 
        scrollableParent === document.documentElement || 
        scrollableParent === document.body) {
      return null;
    }
    
    return scrollableParent;
  }

  function selectFrame(frameElement) {
    const rect = frameElement.getBoundingClientRect();
    
    area = {
      viewportX: rect.left,
      viewportY: rect.top,
      width: rect.width,
      height: rect.height,
      x: rect.left + window.scrollX,
      y: rect.top + window.scrollY
    };
    
    originalCoords = { x: area.x, y: area.y, viewportX: area.viewportX, viewportY: area.viewportY };
    autoscrollTarget = frameElement;
    setupContentObserver(frameElement);

    if (overlay) {
      overlay.remove();
      overlay = null;
    }

    announceToScreenReader(`Frame selected: ${area.width} by ${area.height} pixels`);
    playAccessibilitySound('success');

    showDynamicControl();
    if (frameSettings.enabled && !recorder?.state) createRecordingFrame();
    try {
      chrome?.runtime?.sendMessage?.({type: 'sar-area-selected', area});
    } catch (e) {
      console.debug('[SAR] Chrome runtime not available:', e);
    }

    if (!autoscrollSettings.enabled) {
      autoscrollSettings.enabled = true;
    }
  }

  function setupContentObserver(targetElement) {
    if (contentObserver) contentObserver.disconnect();
    
    contentObserver = new MutationObserver((mutations) => {
      let significantChange = false;
      mutations.forEach((mutation) => {
        if (mutation.type === 'childList' && mutation.addedNodes.length > 0) {
          mutation.addedNodes.forEach((node) => {
            if (node.nodeType === Node.ELEMENT_NODE && 
                (node.tagName === 'IFRAME' || 
                 node.className.toLowerCase().includes('ad') ||
                 node.offsetHeight > 100)) {
              significantChange = true;
            }
          });
        }
      });
      
      if (significantChange) {
        scrollEndCounter = 0;
        lastScrollPos = 0;
      }
    });

    if (targetElement) {
      contentObserver.observe(targetElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ['style', 'class', 'height', 'width']
      });
    }
  }

  function disconnectContentObserver() {
    if (contentObserver) {
      contentObserver.disconnect();
      contentObserver = null;
    }
  }

  function scroll() {
    if (!autoscrollSettings.enabled || !recorder || recorder.state !== 'recording') {
      scrollAnimationId = requestAnimationFrame(scroll);
      return;
    }
    
    const currentTime = performance.now();
    
    if (currentTime - lastScrollTime < autoscrollSettings.delay) {
      scrollAnimationId = requestAnimationFrame(scroll);
      return;
    }

    try {
      const scrollableElement = autoscrollTarget || document.scrollingElement || document.documentElement;
      let currentScroll, maxScroll;

      if (scrollableElement === document.scrollingElement || 
          scrollableElement === document.documentElement || 
          scrollableElement === document.body) {
        
        const currentScrollHeight = Math.max(
          document.body.scrollHeight || 0,
          document.documentElement.scrollHeight || 0
        );
        maxScroll = currentScrollHeight - window.innerHeight;
        currentScroll = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop || 0;
      } else {
        maxScroll = scrollableElement.scrollHeight - scrollableElement.clientHeight;
        currentScroll = scrollableElement.scrollTop;
      }

      if (currentScroll >= maxScroll) {
        scrollEndCounter++;
        if (scrollEndCounter > 30) {
          console.log('[SAR] Auto-stopping: reached end of page');
          stopRec();
          return;
        }
      } else {
        scrollEndCounter = 0;
      }

      if (Math.abs(currentScroll - lastScrollPos) < 1) {
        scrollEndCounter++;
        if (scrollEndCounter > 60) {
          console.log('[SAR] Auto-stopping: scroll stuck (weak computer)');
          stopRec();
          return;
        }
      } else {
        lastScrollPos = currentScroll;
        scrollEndCounter = 0;
      }

      const scrollStep = Math.max(1, Math.floor(autoscrollSettings.speed / 10));
      let newScroll = currentScroll;
      let shouldScroll = false;

      if (autoscrollSettings.direction === 'down' && currentScroll < maxScroll) {
        newScroll = Math.min(currentScroll + scrollStep, maxScroll);
        shouldScroll = true;
      } else if (autoscrollSettings.direction === 'up' && currentScroll > 0) {
        newScroll = Math.max(currentScroll - scrollStep, 0);
        shouldScroll = true;
      }

      if (shouldScroll && Math.abs(newScroll - currentScroll) >= 1) {
        if (scrollableElement === document.scrollingElement || 
            scrollableElement === document.documentElement || 
            scrollableElement === document.body) {
          window.scrollTo(0, newScroll);
        } else {
          scrollableElement.scrollTop = newScroll;
        }
        lastScrollTime = currentTime;
      }
      
      scrollAnimationId = requestAnimationFrame(scroll);

    } catch (e) {
      console.error('[SAR] Scroll error:', e);
      autoscrollSettings.enabled = false;
      const asEnable = toolbarShadow?.querySelector('#as-enable');
      if (asEnable) asEnable.checked = false;
      
      if (scrollAnimationId) {
        cancelAnimationFrame(scrollAnimationId);
        scrollAnimationId = null;
      }
    }
  }

  function startSmoothScroll() {
    if (scrollAnimationId) cancelAnimationFrame(scrollAnimationId);
    lastScrollTime = 0;
    scrollEndCounter = 0;
    lastScrollPos = 0;
    scrollAnimationId = requestAnimationFrame(scroll);
  }

  function stopSmoothScroll() {
    if (scrollAnimationId) {
      cancelAnimationFrame(scrollAnimationId);
      scrollAnimationId = null;
    }
  }

  function down(e) {
    if (!overlay || typeof e.clientX !== 'number' || typeof e.clientY !== 'number') return;
    
    const snapped = snapToGrid(e.clientX, e.clientY);
    sx = snapped.x;
    sy = snapped.y;

    rect = document.createElement('div');
    rect.setAttribute('role', 'presentation');
    rect.setAttribute('aria-label', 'Selection rectangle');
    
    const borderColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : '#0f0';
    const bgColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? 
      'rgba(255,215,0,0.15)' : 'rgba(0,255,0,0.15)';
    const borderWidth = accessibilitySettings.enabled && accessibilitySettings.enhancedBorders ? '3px' : '2px';
    
    rect.style.cssText = `
      position:absolute!important;border:${borderWidth} dashed ${borderColor}!important;
      background:${bgColor}!important;left:${sx}px!important;
      top:${sy}px!important;z-index:2147483644!important;pointer-events:none!important;
    `;
    
    if (gridEnabled) {
      const gridColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? '#FFD700' : '#0096ff';
      const gridBg = accessibilitySettings.enabled && accessibilitySettings.highContrast ? 
        'rgba(255,215,0,0.15)' : 'rgba(0,150,255,0.15)';
      
      rect.style.borderColor = gridColor;
      rect.style.background = gridBg;
      rect.style.boxShadow = `0 0 0 1px ${gridColor}`;
    }
    
    overlay.appendChild(rect);
    overlay.addEventListener('mousemove', move);
    overlay.addEventListener('mouseup', up);
  }

  function move(e) {
    if (!rect) return;
    
    const snapped = snapRectToGrid(sx, sy, e.clientX, e.clientY);
    
    rect.style.left = snapped.x + 'px';
    rect.style.top = snapped.y + 'px';
    rect.style.width = snapped.width + 'px';
    rect.style.height = snapped.height + 'px';
    
    if (gridEnabled && rect.querySelector('.size-info') === null) {
      const sizeInfo = document.createElement('div');
      sizeInfo.className = 'size-info';
      sizeInfo.setAttribute('role', 'status');
      sizeInfo.setAttribute('aria-live', 'polite');
      
      const fontSize = accessibilitySettings.enabled ? '13px' : '11px';
      const bgColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? 
        'rgba(0,0,0,0.95)' : 'rgba(0,150,255,0.9)';
      
      sizeInfo.style.cssText = `
        position:absolute!important;bottom:-30px!important;left:0!important;
        background:${bgColor}!important;color:white!important;
        padding:4px 8px!important;border-radius:4px!important;font-size:${fontSize}!important;
        font-family:Arial,sans-serif!important;white-space:nowrap!important;pointer-events:none!important;
        font-weight:bold!important;text-shadow: 1px 1px 2px rgba(0,0,0,0.8)!important;
        border: 1px solid rgba(255,255,255,0.3)!important;
      `;
      rect.appendChild(sizeInfo);
    }
    
    const sizeInfo = rect.querySelector('.size-info');
    if (sizeInfo) {
      sizeInfo.textContent = `${snapped.width} × ${snapped.height}`;
    }
  }

  async function up(e) {
    if (typeof sx !== 'number' || typeof sy !== 'number') {
      overlay?.remove();
      overlay = null;
      createOverlay();
      return;
    }

    const hint = document.querySelector('div[style*="🎯 Select area"]');
    if (hint?.parentNode) hint.parentNode.removeChild(hint);

    overlay?.remove();
    overlay = null;

    const snapped = snapRectToGrid(sx, sy, e.clientX, e.clientY);
    const viewportX = snapped.x;
    const viewportY = snapped.y;
    const w = snapped.width;
    const h = snapped.height;

    if (w < 20 || h < 20) {
      announceToScreenReader('Area too small, please select a larger area');
      playAccessibilitySound('error');
      createOverlay();
      return;
    }

    const maxHeight = window.innerHeight * 5;
    const finalHeight = Math.min(h, maxHeight);

    area = {
      viewportX: viewportX,
      viewportY: viewportY,
      width: w,
      height: finalHeight,
      x: viewportX + window.scrollX,
      y: viewportY + window.scrollY
    };
    
    originalCoords = { x: area.x, y: area.y, viewportX: area.viewportX, viewportY: area.viewportY };

    if (calibrationMode) {
      startCalibrationRec();
      return;
    }

    const centerX = viewportX + w / 2;
    const centerY = viewportY + finalHeight / 2;
    const el = document.elementFromPoint(centerX, centerY);
    let scrollable = null;

    if (el) scrollable = findScrollableParent(el);

    if (!scrollable && document.scrollingElement && 
        document.documentElement.scrollHeight > window.innerHeight) {
      scrollable = document.scrollingElement;
    }

    if (scrollable) {
      autoscrollTarget = scrollable;
      window.__sarAutoscrollTarget = scrollable;
      setupContentObserver(scrollable);
      
      if (!autoscrollSettings.enabled && scrollable !== document.scrollingElement) {
        autoscrollSettings.enabled = true;
      }
    }

    announceToScreenReader(`Area selected: ${w} by ${finalHeight} pixels`);
    playAccessibilitySound('success');

    try {
      chrome?.runtime?.sendMessage?.({type: 'sar-area-selected', area});
    } catch (e) {
      console.debug('[SAR] Chrome runtime not available:', e);
    }
    showDynamicControl();
    
    if (frameSettings.enabled && !recorder?.state) createRecordingFrame();
  }

  async function startCalibrationRec() {
    try {
      const fullStream = await navigator.mediaDevices.getDisplayMedia({
        video: { cursor: "always", displaySurface: "browser" },
        audio: false
      });
      
      const videoElement = document.createElement('video');
      videoElement.srcObject = fullStream;
      videoElement.muted = true;
      
      await new Promise(resolve => {
        videoElement.onloadedmetadata = () => videoElement.play().then(resolve);
      });

      const scaleX = videoElement.videoWidth / window.innerWidth;
      const scaleY = videoElement.videoHeight / window.outerHeight;
      const offsetY_formula = window.outerHeight - window.innerHeight;

      fullStream.getTracks().forEach(track => track.stop());

      try {
        chrome.storage.sync.set({ sarProOffsetY: offsetY_formula }, () => {
          chrome.runtime.sendMessage({ type: 'sar-calibration-result', offsetY: offsetY_formula });
          calibrationMode = false;
        });
      } catch (e) {
        console.debug('[SAR] Chrome storage/runtime not available:', e);
        calibrationMode = false;
      }
    } catch (err) {
      try {
        chrome.runtime.sendMessage({ type: 'sar-calibration-result' });
      } catch (e) {
        console.debug('[SAR] Chrome runtime not available:', e);
      }
      calibrationMode = false;
    }
  }

  // === SMART SETTINGS PANEL ===
  function panel() {
    if (toolbar?.parentNode) toolbar.parentNode.removeChild(toolbar);

    toolbar = document.createElement('div');
    toolbar.setAttribute('role', 'dialog');
    toolbar.setAttribute('aria-label', 'Screen Area Recorder settings');
    toolbar.setAttribute('aria-modal', 'true');
    
    const minWidth = accessibilitySettings.enabled ? '450px' : '350px';
    const maxWidth = accessibilitySettings.enabled ? '550px' : '450px';
    const borderColor = accessibilitySettings.enabled && accessibilitySettings.highContrast ? 
      '#FFD700' : 'rgba(76,175,80,0.3)';
    const borderWidth = accessibilitySettings.enabled && accessibilitySettings.enhancedBorders ? '3px' : '1px';
    
    toolbar.style.cssText = `
      position:fixed!important;z-index:2147483644!important;
      background:rgba(0,0,0,0.9)!important;border-radius:12px!important;padding:0!important;
      font-family:Arial,sans-serif!important;color:white!important;
      box-shadow:0 8px 24px rgba(0,0,0,0.5)!important;min-width:${minWidth}!important;max-width:${maxWidth}!important;
      border:${borderWidth} solid ${borderColor}!important;max-height:80vh!important;overflow-y:auto!important;
    `;

    if (accessibilitySettings.enabled) {
      toolbar.classList.add('sar-accessibility-enhanced');
    }

    smartPosition(toolbar, area || { viewportX: 20, viewportY: 80, width: 350, height: 400 }, 'floating-top');

    toolbarShadow = toolbar.attachShadow ? toolbar.attachShadow({mode: 'open'}) : toolbar;

    toolbarShadow.innerHTML = `
      <style>
        ${getAccessibilityStyles()}
        .sar-panel{background:linear-gradient(135deg,rgba(0,0,0,0.95),rgba(20,20,20,0.95));border-radius:12px;padding:0;font-family:Arial,sans-serif;color:white;width:100%;overflow:hidden;max-height:80vh;overflow-y:auto}
        .sar-header{background:linear-gradient(135deg,#4CAF50,#45a049);padding:15px 20px;text-align:center;border-radius:12px 12px 0 0}
        .sar-title{font-size:16px;font-weight:bold;margin:0;color:white}
        .sar-area-info{font-size:12px;margin:8px 0 0;opacity:0.9;background:rgba(255,255,255,0.1);padding:8px;border-radius:6px;margin-top:8px}
        .sar-main-controls{padding:20px;background:rgba(255,255,255,0.02);border-bottom:1px solid rgba(255,255,255,0.1)}
        .sar-buttons{display:flex;gap:10px;justify-content:center;margin-bottom:15px;flex-wrap:wrap}
        .sar-btn{padding:10px 15px;border:none;border-radius:8px;cursor:pointer;font-size:12px;font-weight:bold;transition:all 0.3s ease;box-shadow:0 2px 8px rgba(0,0,0,0.2)}
        .sar-btn:hover{transform:translateY(-2px);box-shadow:0 4px 12px rgba(0,0,0,0.3)}
        .sar-btn:disabled{opacity:0.4;cursor:not-allowed;transform:none}
        .sar-btn:focus{outline:3px solid #4CAF50;outline-offset:2px}
        .btn-start{background:linear-gradient(135deg,#4CAF50,#45a049);color:white}
        .btn-stop{background:linear-gradient(135deg,#f44336,#d32f2f);color:white}
        .btn-pause{background:linear-gradient(135deg,#ff9800,#f57c00);color:white}
        .btn-edit{background:linear-gradient(135deg,#2196F3,#1976D2);color:white}
        .btn-close{background:linear-gradient(135deg,#666,#555);color:white}
        .sar-hotkeys{font-size:10px;opacity:0.7;text-align:center;background:rgba(255,255,255,0.05);padding:8px;border-radius:6px;margin-top:10px}
        .accordion-section{border-bottom:1px solid rgba(255,255,255,0.1)}
        .accordion-header{padding:15px 20px;cursor:pointer;display:flex;justify-content:space-between;align-items:center;background:rgba(255,255,255,0.03);transition:all 0.3s ease;user-select:none}
        .accordion-header:hover{background:rgba(255,255,255,0.08)}
        .accordion-header:focus{outline:3px solid #4CAF50;outline-offset:2px}
        .accordion-header.active{background:rgba(255,255,255,0.1)}
        .accordion-title{font-weight:bold;font-size:13px;display:flex;align-items:center;gap:8px}
        .accordion-arrow{font-size:12px;transition:transform 0.3s ease;color:rgba(255,255,255,0.6)}
        .accordion-header.active .accordion-arrow{transform:rotate(90deg)}
        .accordion-content{max-height:0;overflow:hidden;transition:max-height 0.3s ease;background:rgba(255,255,255,0.02)}
        .accordion-content.active{max-height:400px}
        .accordion-inner{padding:15px 20px}
        .sar-controls{display:flex;gap:12px;align-items:center;margin-bottom:12px;flex-wrap:wrap}
        .sar-controls label{display:flex;align-items:center;gap:6px;font-size:11px}
        .sar-controls input[type="range"]{width:80px!important;background:#444!important;accent-color:#4CAF50!important}
        .sar-controls input[type="checkbox"]{margin:0!important;accent-color:#4CAF50!important;transform:scale(1.2)}
        .sar-controls input[type="checkbox"]:focus{outline:3px solid #4CAF50!important;outline-offset:2px!important}
        .sar-controls select{padding:4px 8px!important;font-size:11px!important;background:#333!important;color:#fff!important;border:1px solid #555!important;border-radius:4px!important}
        .sar-controls select:focus{outline:3px solid #4CAF50!important;outline-offset:2px!important}
        .sar-controls input[type="number"]{width:60px!important;background:#333!important;color:#fff!important;border:1px solid #555!important;border-radius:4px!important;padding:4px 6px!important;font-size:11px!important}
        .sar-controls input[type="number"]:focus{outline:3px solid #4CAF50!important;outline-offset:2px!important}
        .sar-controls input[type="range"]:focus{outline:3px solid #4CAF50!important;outline-offset:2px!important}
        .format-buttons{display:flex;gap:8px;margin-top:10px}
        .format-btn{padding:8px 12px;border:none;border-radius:6px;cursor:pointer;font-size:11px;font-weight:bold;transition:all 0.3s ease}
        .format-btn:focus{outline:3px solid #4CAF50!important;outline-offset:2px!important}
        .format-btn.active{background:linear-gradient(135deg,#9c27b0,#7b1fa2);color:white}
        .format-btn:not(.active){background:#444;color:#ccc}
        .format-btn:hover:not(.active){background:#555}
        .status-text{color:#4CAF50;font-weight:bold}
        .description{font-size:9px;opacity:0.7;margin-top:8px;line-height:1.3}
        .audio-test-btn{padding:4px 8px;background:#2196F3;color:white;border:none;border-radius:4px;cursor:pointer;font-size:10px;margin-left:8px}
        .audio-test-btn:focus{outline:3px solid #4CAF50!important;outline-offset:2px!important}
        .volume-control{display:flex;align-items:center;gap:8px;margin-top:8px}
        .accessibility-tip{background:linear-gradient(135deg,#FFD700,#FFA000);color:#000;padding:10px;border-radius:8px;margin-top:15px;font-size:11px;text-align:center;font-weight:bold}
      </style>
      <div class="sar-panel">
        <div class="sar-header">
          <div class="sar-title">🎥 Screen Area Recorder Pro</div>
          <div class="sar-area-info">
            🎯 Selected area: ${area ? `${area.width}×${area.height} at (${area.viewportX}, ${area.viewportY})` : 'No area selected'}
            <br><small>✏️ Click Edit to resize/move area • No countdown • Clean recording</small>
          </div>
        </div>

        <div class="sar-main-controls">
          <div class="sar-buttons">
            <button id="bS" class="sar-btn btn-start" tabindex="0" aria-label="Start recording">▶ Start Recording</button>
            <button id="bT" class="sar-btn btn-stop" disabled tabindex="0" aria-label="Stop recording">⏹ Stop</button>
            <button id="bP" class="sar-btn btn-pause" disabled tabindex="0" aria-label="Pause recording">⏸ Pause</button>
            <button id="bEdit" class="sar-btn btn-edit" tabindex="0" aria-label="Edit recording area">✏ Edit Area</button>
            <button id="bClose" class="sar-btn btn-close" tabindex="0" aria-label="Close settings">✕ Close</button>
          </div>
          <div class="sar-hotkeys">
            🎮 Hotkeys: Ctrl+Space (Start/Stop) | ESC (Stop) | Ctrl+Shift+P (Pause) | Ctrl+A (Full page) | H (Hide/Show Panel)
          </div>
          ${!supportsElementCapture ? `
          <div style="background:rgba(255,152,0,0.2);border:1px solid #ff9800;border-radius:6px;padding:8px;margin-top:8px;font-size:10px;text-align:center">
            ⚠️ <strong>Legacy Mode</strong><br>
            Panel may be recorded. Use <strong>H key</strong> to hide/show controls during recording.
            <br><small>Upgrade to Chrome 132+ for perfect Element Capture</small>
          </div>
          ` : `
          <div style="background:rgba(76,175,80,0.2);border:1px solid #4CAF50;border-radius:6px;padding:8px;margin-top:8px;font-size:10px;text-align:center">
            ✅ <strong>Element Capture Enabled</strong><br>
            Panel will NEVER be recorded with advanced API.
            <br><small>Chrome 132+ detected</small>
          </div>
          `}
          <div class="accessibility-tip">
            ♿ <strong>Accessibility:</strong> Use the ${accessibilitySettings.enabled ? '♿' : '👁'} button in the mini-panel to toggle large fonts, high contrast, and audio feedback
          </div>
        </div>

        <div class="accordion-section">
          <div class="accordion-header" data-target="audio" tabindex="0" role="button" aria-expanded="false" aria-controls="audio-content">
            <div class="accordion-title">🎤 <span>Audio Recording</span> <span class="status-text">${audioSettings.enabled ? audioSettings.source.toUpperCase() : 'OFF'}</span></div>
            <span class="accordion-arrow">▶</span>
          </div>
          <div class="accordion-content" id="audio-content" role="region" aria-labelledby="audio-header">
            <div class="accordion-inner">
              <div class="sar-controls">
                <label><input type="checkbox" id="audio-enable" ${audioSettings.enabled ? 'checked' : ''} aria-describedby="audio-desc">Enable Audio</label>
                <button id="audio-test-btn" class="audio-test-btn" tabindex="0" aria-label="Test audio setup">🎵 Test</button>
              </div>
              <div class="sar-controls">
                <label>Source: 
                  <select id="audio-source" aria-label="Audio source">
                    <option value="microphone" ${audioSettings.source === 'microphone' ? 'selected' : ''}>🎤 Microphone</option>
                    <option value="system" ${audioSettings.source === 'system' ? 'selected' : ''}>🔊 System Audio</option>
                    <option value="both" ${audioSettings.source === 'both' ? 'selected' : ''}>🎤🔊 Both</option>
                  </select>
                </label>
              </div>
              <div class="volume-control">
                <label>🎤 Volume: <input type="range" id="mic-volume" min="0" max="200" value="${audioSettings.micVolume}" aria-label="Microphone volume"></label>
                <span id="mic-volume-value" aria-live="polite">${audioSettings.micVolume}%</span>
              </div>
              <div class="volume-control">
                <label>🔊 Volume: <input type="range" id="system-volume" min="0" max="200" value="${audioSettings.systemVolume}" aria-label="System audio volume"></label>
                <span id="system-volume-value" aria-live="polite">${audioSettings.systemVolume}%</span>
              </div>
              <div class="description" id="audio-desc">🎤 Microphone: Record your voice commentary<br>🔊 System Audio: Record browser tab sounds (Chrome only)<br>🎤🔊 Both: Mix microphone + system audio</div>
            </div>
          </div>
        </div>
        
        <div class="accordion-section">
          <div class="accordion-header" data-target="format" tabindex="0" role="button" aria-expanded="false" aria-controls="format-content">
            <div class="accordion-title">🎬 <span>Recording Format</span> <span class="status-text">${fmt.toUpperCase()}</span></div>
            <span class="accordion-arrow">▶</span>
          </div>
          <div class="accordion-content" id="format-content" role="region" aria-labelledby="format-header">
            <div class="accordion-inner">
              <div class="format-buttons" role="radiogroup" aria-label="Recording format">
                <button class="format-btn ${fmt === 'webm' ? 'active' : ''}" data-format="webm" role="radio" aria-checked="${fmt === 'webm'}" tabindex="0">WebM</button>
                <button class="format-btn ${fmt === 'mp4' ? 'active' : ''}" data-format="mp4" role="radio" aria-checked="${fmt === 'mp4'}" tabindex="0">MP4</button>
              </div>
              <div class="description">WebM: Best quality, smaller files, modern browsers<br>MP4: Universal compatibility, larger files, all devices</div>
            </div>
          </div>
        </div>

        <div class="accordion-section">
          <div class="accordion-header" data-target="autoscroll" tabindex="0" role="button" aria-expanded="false" aria-controls="autoscroll-content">
            <div class="accordion-title">📜 <span>Auto-scroll Settings</span> <span class="status-text">${autoscrollSettings.enabled ? 'ON' : 'OFF'}</span></div>
            <span class="accordion-arrow">▶</span>
          </div>
          <div class="accordion-content" id="autoscroll-content" role="region" aria-labelledby="autoscroll-header">
            <div class="accordion-inner">
              <div class="sar-controls">
                <label><input type="checkbox" id="as-enable" ${autoscrollSettings.enabled ? 'checked' : ''}>Enable Auto-scroll</label>
              </div>
              <div class="sar-controls">
                <label>Speed: <input type="range" id="as-speed" min="1" max="300" value="${autoscrollSettings.speed}" aria-label="Scroll speed"></label>
                <input type="number" id="speed-input" min="1" max="300" value="${autoscrollSettings.speed}" aria-label="Speed value">
              </div>
              <div class="sar-controls">
                <label>Direction: 
                  <select id="as-direction" aria-label="Scroll direction">
                    <option value="down" ${autoscrollSettings.direction === 'down' ? 'selected' : ''}>Down ⬇</option>
                    <option value="up" ${autoscrollSettings.direction === 'up' ? 'selected' : ''}>Up ⬆</option>
                  </select>
                </label>
                <label>Delay: <input type="range" id="as-delay" min="50" max="1000" value="${autoscrollSettings.delay}" aria-label="Scroll delay"></label>
                <input type="number" id="delay-input" min="50" max="1000" value="${autoscrollSettings.delay}" aria-label="Delay value">ms
              </div>
              <div class="description">⚡ Auto-optimizes scroll smoothness during recording.<br>Perfect for capturing long pages or social media feeds.</div>
            </div>
          </div>
        </div>

        <div class="accordion-section">
          <div class="accordion-header" data-target="frame" tabindex="0" role="button" aria-expanded="false" aria-controls="frame-content">
            <div class="accordion-title">🎯 <span>Recording Frame</span></div>
            <span class="accordion-arrow">▶</span>
          </div>
          <div class="accordion-content" id="frame-content" role="region" aria-labelledby="frame-header">
            <div class="accordion-inner">
              <div class="sar-controls">
                <label><input type="checkbox" id="frame-enable" ${frameSettings.enabled ? 'checked' : ''}>Show Frame</label>
                <label><input type="checkbox" id="frame-hide-record" ${frameSettings.hideOnRecord ? 'checked' : ''}>Hide During Recording</label>
              </div>
              <div class="description">🔒 Fixed viewport mode: Frame position never changes, content scrolls under it.<br>Frame helps you see exactly what area is being recorded.</div>
            </div>
          </div>
        </div>
      </div>
    `;

    document.body.appendChild(toolbar);
    setupPanelEvents();
    
    announceToScreenReader('Settings panel opened');
    playAccessibilitySound('notification');
    
    setTimeout(() => {
      const firstButton = toolbarShadow.querySelector('#bS');
      if (firstButton) firstButton.focus();
    }, 100);
  }

  function setupPanelEvents() {
    // Accordion functionality with keyboard support
    const accordionHeaders = toolbarShadow.querySelectorAll('.accordion-header');
    accordionHeaders.forEach(header => {
      header.onclick = () => toggleAccordion(header);
      header.onkeydown = (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          toggleAccordion(header);
        }
      };
    });

    function toggleAccordion(header) {
      const target = header.dataset.target;
      const content = toolbarShadow.querySelector(`#${target}-content`);
      const isActive = header.classList.contains('active');
      
      accordionHeaders.forEach(h => {
        h.classList.remove('active');
        h.setAttribute('aria-expanded', 'false');
        const c = toolbarShadow.querySelector(`#${h.dataset.target}-content`);
        if (c) c.classList.remove('active');
      });
      
      if (!isActive) {
        header.classList.add('active');
        header.setAttribute('aria-expanded', 'true');
        content.classList.add('active');
        
        announceToScreenReader(`${header.querySelector('.accordion-title span').textContent} section opened`);
        playAccessibilitySound('notification');
      } else {
        announceToScreenReader(`${header.querySelector('.accordion-title span').textContent} section closed`);
      }
    }

    // Accessibility controls
    const accessibilityEnable = toolbarShadow.querySelector('#accessibility-enable');
    if (accessibilityEnable) {
      accessibilityEnable.onchange = () => {
        accessibilitySettings.enabled = accessibilityEnable.checked;
        saveSettings('Accessibility', accessibilitySettings);
        applyAccessibilityStyles();
      };
    }

    // Audio controls
    const audioEnable = toolbarShadow.querySelector('#audio-enable');
    const audioSource = toolbarShadow.querySelector('#audio-source');
    const micVolume = toolbarShadow.querySelector('#mic-volume');
    const systemVolume = toolbarShadow.querySelector('#system-volume');
    const micVolumeValue = toolbarShadow.querySelector('#mic-volume-value');
    const systemVolumeValue = toolbarShadow.querySelector('#system-volume-value');
    const audioTestBtn = toolbarShadow.querySelector('#audio-test-btn');
    
    if (audioEnable) {
      audioEnable.onchange = () => {
        audioSettings.enabled = audioEnable.checked;
        saveSettings('Audio', audioSettings);
        
        const statusSpan = toolbarShadow.querySelector('.accordion-header[data-target="audio"] .status-text');
        if (statusSpan) statusSpan.textContent = audioSettings.enabled ? audioSettings.source.toUpperCase() : 'OFF';
        updateDynamicControlContent(recorder?.state === 'recording', recorder?.state === 'paused');
        
        announceToScreenReader(`Audio recording ${audioSettings.enabled ? 'enabled' : 'disabled'}`);
      };
    }

    if (audioSource) {
      audioSource.onchange = () => {
        audioSettings.source = audioSource.value;
        saveSettings('Audio', audioSettings);
        
        const statusSpan = toolbarShadow.querySelector('.accordion-header[data-target="audio"] .status-text');
        if (statusSpan) statusSpan.textContent = audioSettings.enabled ? audioSettings.source.toUpperCase() : 'OFF';
        updateDynamicControlContent(recorder?.state === 'recording', recorder?.state === 'paused');
        
        announceToScreenReader(`Audio source changed to ${audioSettings.source}`);
      };
    }

    if (micVolume && micVolumeValue) {
      micVolume.oninput = () => {
        const value = parseInt(micVolume.value, 10);
        audioSettings.micVolume = value;
        micVolumeValue.textContent = value + '%';
        updateAudioVolume();
        saveSettings('Audio', audioSettings);
      };
    }

    if (systemVolume && systemVolumeValue) {
      systemVolume.oninput = () => {
        const value = parseInt(systemVolume.value, 10);
        audioSettings.systemVolume = value;
        systemVolumeValue.textContent = value + '%';
        updateAudioVolume();
        saveSettings('Audio', audioSettings);
      };
    }

            if (audioTestBtn) {
      audioTestBtn.onclick = async () => {
        try {
          audioTestBtn.textContent = '🔄 Testing...';
          audioTestBtn.disabled = true;
          announceToScreenReader('Testing audio setup');
          
          const testStream = await setupAudioStream();
          if (testStream) {
            testStream.getTracks().forEach(track => track.stop());
            announceToScreenReader('Audio test successful');
            playAccessibilitySound('success');
          }
        } catch (err) {
          console.error('[SAR] Audio test error:', err);
          announceToScreenReader('Audio test failed');
          playAccessibilitySound('error');
        } finally {
          audioTestBtn.textContent = '🎵 Test';
          audioTestBtn.disabled = false;
        }
      };
    }
    
    // Frame controls
    const frameEnable = toolbarShadow.querySelector('#frame-enable');
    const frameHideRecord = toolbarShadow.querySelector('#frame-hide-record');
    
    if (frameEnable) {
      frameEnable.onchange = () => {
        frameSettings.enabled = frameEnable.checked;
        saveSettings('Frame', frameSettings);
        
        if (frameSettings.enabled && area && recorder?.state !== 'recording') {
          createRecordingFrame();
        } else if (!frameSettings.enabled) {
          removeRecordingFrame();
        }
        
        announceToScreenReader(`Recording frame ${frameSettings.enabled ? 'enabled' : 'disabled'}`);
      };
    }

    if (frameHideRecord) {
      frameHideRecord.onchange = () => {
        frameSettings.hideOnRecord = frameHideRecord.checked;
        saveSettings('Frame', frameSettings);
        announceToScreenReader(`Frame will ${frameSettings.hideOnRecord ? 'hide' : 'show'} during recording`);
      };
    }

    // Format buttons with keyboard support
    const formatButtons = toolbarShadow.querySelectorAll('.format-btn');
    formatButtons.forEach((btn, index) => {
      btn.onclick = () => selectFormat(btn);
      btn.onkeydown = (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          selectFormat(btn);
        } else if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
          e.preventDefault();
          const nextIndex = e.key === 'ArrowLeft' ? 
            (index - 1 + formatButtons.length) % formatButtons.length :
            (index + 1) % formatButtons.length;
          formatButtons[nextIndex].focus();
        }
      };
    });

    function selectFormat(btn) {
      const newFormat = btn.dataset.format;
      if (newFormat && newFormat !== fmt) {
        fmt = newFormat;
        saveSettings('Format', { format: fmt, quality: settings });
        
        formatButtons.forEach(b => {
          b.classList.remove('active');
          b.setAttribute('aria-checked', 'false');
        });
        btn.classList.add('active');
        btn.setAttribute('aria-checked', 'true');
        
        const statusSpan = toolbarShadow.querySelector('.accordion-header[data-target="format"] .status-text');
        if (statusSpan) statusSpan.textContent = fmt.toUpperCase();
        
        announceToScreenReader(`Format changed to ${fmt.toUpperCase()}`);
        playAccessibilitySound('notification');
        
        try {
          chrome?.runtime?.sendMessage?.({ type: 'sar-format-changed', format: fmt });
        } catch (e) {
          console.debug('[SAR] Chrome runtime not available:', e);
        }
      }
    }

    // Autoscroll controls
    const asEnable = toolbarShadow.querySelector('#as-enable');
    const asSpeed = toolbarShadow.querySelector('#as-speed');
    const asDirection = toolbarShadow.querySelector('#as-direction');
    const asDelay = toolbarShadow.querySelector('#as-delay');
    const speedInput = toolbarShadow.querySelector('#speed-input');
    const delayInput = toolbarShadow.querySelector('#delay-input');

    if (asEnable) {
      asEnable.onchange = () => {
        autoscrollSettings.enabled = asEnable.checked;
        saveSettings('Autoscroll', autoscrollSettings);
        
        const statusSpan = toolbarShadow.querySelector('.accordion-header[data-target="autoscroll"] .status-text');
        if (statusSpan) statusSpan.textContent = autoscrollSettings.enabled ? 'ON' : 'OFF';
        
        announceToScreenReader(`Auto-scroll ${autoscrollSettings.enabled ? 'enabled' : 'disabled'}`);
      };
    }

    if (asSpeed && speedInput) {
      asSpeed.oninput = () => {
        const value = parseInt(asSpeed.value, 10);
        autoscrollSettings.speed = value;
        speedInput.value = value;
        saveSettings('Autoscroll', autoscrollSettings);
      };
      
      speedInput.oninput = () => {
        let value = parseInt(speedInput.value, 10);
        if (isNaN(value)) value = 100;
        value = Math.max(1, Math.min(300, value));
        autoscrollSettings.speed = value;
        asSpeed.value = value;
        speedInput.value = value;
        saveSettings('Autoscroll', autoscrollSettings);
      };
    }

    if (asDirection) {
      asDirection.onchange = () => {
        autoscrollSettings.direction = asDirection.value;
        saveSettings('Autoscroll', autoscrollSettings);
        announceToScreenReader(`Scroll direction changed to ${autoscrollSettings.direction}`);
      };
    }

    if (asDelay && delayInput) {
      asDelay.oninput = () => {
        const value = parseInt(asDelay.value, 10);
        autoscrollSettings.delay = value;
        delayInput.value = value;
        saveSettings('Autoscroll', autoscrollSettings);
      };
      
      delayInput.oninput = () => {
        let value = parseInt(delayInput.value, 10);
        if (isNaN(value)) value = 100;
        value = Math.max(50, Math.min(1000, value));
        autoscrollSettings.delay = value;
        asDelay.value = value;
        delayInput.value = value;
        saveSettings('Autoscroll', autoscrollSettings);
      };
    }

    // Main control buttons with enhanced accessibility
    const btnStart = toolbarShadow.querySelector('#bS');
    const btnStop = toolbarShadow.querySelector('#bT');
    const btnPause = toolbarShadow.querySelector('#bP');
    const btnEdit = toolbarShadow.querySelector('#bEdit');
    const btnClose = toolbarShadow.querySelector('#bClose');
    
    // Add keyboard support for all buttons
    [btnStart, btnStop, btnPause, btnEdit, btnClose].forEach(btn => {
      if (btn) {
        btn.onkeydown = (e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            btn.click();
          }
        };
      }
    });
    
    if (btnStart) btnStart.onclick = () => {
      announceToScreenReader('Starting recording');
      playAccessibilitySound('start');
      startRec();
    };
    
    if (btnStop) btnStop.onclick = () => {
      announceToScreenReader('Stopping recording');
      playAccessibilitySound('stop');
      stopRec();
    };
    
    if (btnPause) {
      btnPause.onclick = () => {
        if (recorder?.state === 'recording') {
          announceToScreenReader('Recording paused');
          playAccessibilitySound('notification');
          recorder.pause();
          btnPause.textContent = '▶ Resume';
          btnPause.setAttribute('aria-label', 'Resume recording');
          updateDynamicControlContent(true, true);
        } else if (recorder?.state === 'paused') {
          announceToScreenReader('Recording resumed');
          playAccessibilitySound('notification');
          recorder.resume();
          btnPause.textContent = '⏸ Pause';
          btnPause.setAttribute('aria-label', 'Pause recording');
          updateDynamicControlContent(true, false);
        }
      };
    }
    
    if (btnEdit) {
      btnEdit.onclick = () => {
        if (area) {
          announceToScreenReader('Opening area editor');
          playAccessibilitySound('notification');
          if (toolbar) toolbar.style.display = 'none';
          createAreaEditor();
        }
      };
    }
    
    if (btnClose) btnClose.onclick = () => {
      announceToScreenReader('Closing settings');
      playAccessibilitySound('notification');
      cleanup();
    };
  }

  // === HOTKEYS ===
  function setupHotkeys() {
    document.addEventListener('keydown', (e) => {
      // Area editing hotkeys
      if (isEditingArea && areaEditor) {
        if (e.key === 'Enter') { 
          saveAreaEditing(); 
          e.preventDefault(); 
          return; 
        }
        if (e.key === 'Escape') { 
          cancelAreaEditing(); 
          e.preventDefault(); 
          return; 
        }
        
        // Keyboard nudge with accessibility feedback
        const fast = e.shiftKey, step = fast ? KEY_NUDGE_FAST : KEY_NUDGE;
        let dx = 0, dy = 0;
        
        switch(e.key) {
          case 'ArrowLeft': dx = -step; break;
          case 'ArrowRight': dx = step; break;
          case 'ArrowUp': dy = -step; break;
          case 'ArrowDown': dy = step; break;
          default: return;
        }
        
        e.preventDefault();
        let newX = parseInt(areaEditor.style.left) + dx;
        let newY = parseInt(areaEditor.style.top) + dy;
        [newX, newY] = applyGridSnapXY(newX, newY);

        newX = Math.max(0, Math.min(newX, window.innerWidth - area.width));
        newY = Math.max(0, Math.min(newY, window.innerHeight - area.height));

        areaEditor.style.left = newX + 'px';
        areaEditor.style.top = newY + 'px';

        area.viewportX = newX; area.viewportY = newY;
        area.x = newX + window.scrollX; area.y = newY + window.scrollY;

        updateAreaInfo();
        syncEditingUI();
        
        if (accessibilitySettings.enabled && accessibilitySettings.announcements) {
          const direction = dx < 0 ? 'left' : dx > 0 ? 'right' : dy < 0 ? 'up' : 'down';
          announceToScreenReader(`Moved ${direction} ${Math.abs(dx + dy)} pixels`);
        }
        return;
      }

      // Global hotkeys
      if (e.code === 'Escape') {
        if (recorder?.state === 'recording') {
          e.preventDefault();
          announceToScreenReader('Recording stopped by escape key');
          playAccessibilitySound('stop');
          stopRec();
        }
        return;
      }

      if (e.code === 'KeyH' && !e.ctrlKey && !e.shiftKey && !e.altKey) {
        e.preventDefault();
        togglePanelVisibility();
        return;
      }

      if (e.ctrlKey && e.code === 'Space') {
        e.preventDefault();
        if (!recorder || recorder.state !== 'recording') {
          if (area) {
            announceToScreenReader('Starting recording with hotkey');
            playAccessibilitySound('start');
            startRec();
          }
        } else {
          announceToScreenReader('Stopping recording with hotkey');
          playAccessibilitySound('stop');
          stopRec();
        }
        return;
      }

      if (e.ctrlKey && e.shiftKey && e.code === 'KeyP') {
        e.preventDefault();
        if (recorder?.state === 'recording') {
          announceToScreenReader('Recording paused with hotkey');
          playAccessibilitySound('notification');
          recorder.pause();
          updateDynamicControlContent(true, true);
        } else if (recorder?.state === 'paused') {
          announceToScreenReader('Recording resumed with hotkey');
          playAccessibilitySound('notification');
          recorder.resume();
          updateDynamicControlContent(true, false);
        }
      }
    });
  }

  function togglePanelVisibility() {
    if (!toolbar) return;
    
    const isHidden = toolbar.style.display === 'none';
    toolbar.style.display = isHidden ? 'block' : 'none';
    
    announceToScreenReader(`Settings panel ${isHidden ? 'shown' : 'hidden'}`);
    playAccessibilitySound('notification');
  }

  // === CLEANUP ===
  function cleanup() {
    if (recorder?.state === 'recording') stopRec();
    
    removeDynamicControl();
    removeRecordingFrame();
    removeAreaEditor();
    disconnectContentObserver();
    stopFrameCheck();
    removeGridOverlay();
    stopAudioStreams();
    removeCursorHideOverlay();
    
    // Remove accessibility styles
    const accessibilityStyle = document.getElementById('sar-accessibility-styles');
    if (accessibilityStyle) accessibilityStyle.remove();
    
    // Remove announcer
    const announcer = document.getElementById('sar-announcer');
    if (announcer) announcer.remove();
    
    if (fullPageSelectionListener) {
      document.removeEventListener('keydown', fullPageSelectionListener);
      fullPageSelectionListener = null;
    }
    
    if (toolbar?.parentNode) {
      toolbar.parentNode.removeChild(toolbar);
      toolbar = null;
      toolbarShadow = null;
    }
    
    area = null;
    originalCoords = null;
    autoscrollTarget = null;
    
    announceToScreenReader('Screen Area Recorder closed');
    playAccessibilitySound('notification');
  }

  // === MESSAGE LISTENERS ===
  if (chrome?.runtime?.onMessage) {
    chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
      try {
        if (msg.fmt && msg.fmt !== fmt) fmt = msg.fmt;
        if (msg.quality && msg.quality !== settings) settings = msg.quality;
        
        const responses = {
          'sar-select-area': () => { 
            createOverlay(); 
            return {ok: true, currentFormat: fmt};
          },
          'sar-update-grid': () => {
            gridEnabled = !!msg.enabled;
            gridStep = parseInt(msg.step, 10) || 10;
            saveSettings('Grid', {enabled: gridEnabled, step: gridStep});
            gridEnabled ? createGridOverlay() : removeGridOverlay();
            return {ok: true};
          },
          'sar-calibration-start': () => { 
            calibrationMode = true; 
            createOverlay(); 
            return {ok: true}; 
          },
          'sar-start-record': () => {
            if (msg.area) area = msg.area;
            if (msg.quality) settings = msg.quality;
            if (msg.fmt) fmt = msg.fmt;
            startRec();
            return {ok: true, format: fmt};
          },
          'sar-stop-record': () => { 
            stopRec(); 
            return {ok: true}; 
          },
          'sar-autoscroll-settings': () => {
            if (msg.settings) {
              Object.assign(autoscrollSettings, msg.settings);
              saveSettings('Autoscroll', autoscrollSettings);
            }
            return {ok: true};
          },
          'sar-update-format': () => {
            if (msg.format) fmt = msg.format;
            if (msg.quality) settings = msg.quality;
            
            if (toolbar && toolbarShadow) {
              const formatButtons = toolbarShadow.querySelectorAll('.format-btn');
              formatButtons.forEach(btn => {
                btn.classList.toggle('active', btn.dataset.format === fmt);
                btn.setAttribute('aria-checked', btn.dataset.format === fmt);
              });
              
              const statusSpan = toolbarShadow.querySelector('.accordion-header[data-target="format"] .status-text');
              if (statusSpan) statusSpan.textContent = fmt.toUpperCase();
            }
            return {ok: true, format: fmt};
          },
          'sar-frame-settings': () => {
            if (msg.settings) {
              Object.assign(frameSettings, msg.settings);
              saveSettings('Frame', frameSettings);
              
              if (frameSettings.enabled && area && recorder?.state !== 'recording') {
                createRecordingFrame();
              } else if (!frameSettings.enabled) {
                removeRecordingFrame();
              }
            }
            return {ok: true};
          },
          'sar-audio-settings': () => {
            if (msg.settings) {
              Object.assign(audioSettings, msg.settings);
              saveSettings('Audio', audioSettings);
            }
            return {ok: true};
          },
          'sar-accessibility-settings': () => {
            if (msg.settings) {
              Object.assign(accessibilitySettings, msg.settings);
              saveSettings('Accessibility', accessibilitySettings);
              applyAccessibilityStyles();
            }
            return {ok: true};
          }
        };

        const handler = responses[msg?.type] || responses[msg?.cmd];
        sendResponse?.(handler ? handler() : {ok: true});
      } catch (error) {
        console.error('[SAR] Message handler error:', error);
        sendResponse?.({ok: false, error: error.message});
      }
    });
  }

  // === INITIALIZATION ===
  Promise.all([
    loadSettings('Grid', {enabled: gridEnabled, step: gridStep}),
    loadSettings('Autoscroll', autoscrollSettings),
    loadSettings('Frame', frameSettings),
    loadSettings('Format', {format: fmt, quality: settings}),
    loadSettings('Audio', audioSettings),
    loadSettings('Accessibility', accessibilitySettings)
  ]).then(() => {
    setupHotkeys();
    checkElementCaptureSupport();
    applyAccessibilityStyles();
    
    console.log(`✅ SAR Pro initialized with full accessibility support!`);
    console.log(`🎥 Format: ${fmt.toUpperCase()}`);
    console.log(`🎤 Audio: ${audioSettings.enabled ? `${audioSettings.source} (${audioSettings.micVolume}%/${audioSettings.systemVolume}%)` : 'disabled'}`);
    console.log(`♿ Accessibility: ${accessibilitySettings.enabled ? 'enabled' : 'disabled'}`);
    
    if (supportsElementCapture) {
      console.log(`🚀 Element Capture API: SUPPORTED (Chrome 132+)`);
    } else {
      console.log(`⚠️ Element Capture API: NOT SUPPORTED - Using Legacy Mode`);
    }
    
    // Debug functions
    window.sarShowSelectedArea = function() {
      if (!area) {
        console.log('No area selected yet');
        announceToScreenReader('No area selected yet');
        return;
      }
      
      const debugOverlay = document.createElement('div');
      debugOverlay.style.cssText = `
        position:fixed;left:${area.viewportX}px;top:${area.viewportY}px;
        width:${area.width}px;height:${area.height}px;
        border:3px solid red;background:rgba(255,0,0,0.2);
        z-index:2147483647;pointer-events:none;
      `;
      debugOverlay.innerHTML = '<div style="background:red;color:white;padding:2px;font-size:12px;">SELECTED RECORDING AREA</div>';
      document.body.appendChild(debugOverlay);
      
      setTimeout(() => debugOverlay.remove(), 5000);
      console.log('Red overlay shows your selected recording area');
      announceToScreenReader('Debug overlay showing selected recording area');
    };
    
    window.sarDebugInfo = function() {
      console.group('🎯 SAR Pro Debug Info');
      console.log('Element Capture API supported:', supportsElementCapture);
      console.log('Area:', area);
      console.log('Control visible:', controlVisible);
      console.log('Format:', fmt);
      console.log('Audio settings:', audioSettings);
      console.log('Accessibility settings:', accessibilitySettings);
      console.log('Autoscroll target:', autoscrollTarget);
      console.log('Grid enabled:', gridEnabled);
      console.log('Frame settings:', frameSettings);
      console.groupEnd();
    };
    
    window.sarToggleAccessibility = function() {
      // Переключаем accessibility режим
      accessibilitySettings.enabled = !accessibilitySettings.enabled;
      
      // Автоматически включаем все accessibility функции
      if (accessibilitySettings.enabled) {
        accessibilitySettings.highContrast = true;
        accessibilitySettings.enhancedBorders = true;
        accessibilitySettings.audioFeedback = true;
        accessibilitySettings.announcements = true;
        accessibilitySettings.fontSize = 'large';
      }
      
      saveSettings('Accessibility', accessibilitySettings);
      applyAccessibilityStyles();
      
      // Обновляем все интерфейсы
      if (dynamicControl) {
        updateDynamicControlContent(recorder?.state === 'recording', recorder?.state === 'paused');
      }
      
      const status = accessibilitySettings.enabled ? 'enabled' : 'disabled';
      announceToScreenReader(`Accessibility mode ${status}`);
      playAccessibilitySound(accessibilitySettings.enabled ? 'success' : 'notification');
      
      showNotification(
        accessibilitySettings.enabled ? 
          '♿ Accessibility mode enabled: Large fonts, high contrast, audio feedback' :
          'Accessibility mode disabled',
        accessibilitySettings.enabled ? 'success' : 'info'
      );
      
      console.log(`Accessibility mode: ${status}`);
      console.log('- High contrast:', accessibilitySettings.highContrast);
      console.log('- Enhanced borders:', accessibilitySettings.enhancedBorders);
      console.log('- Audio feedback:', accessibilitySettings.audioFeedback);
      console.log('- Screen reader announcements:', accessibilitySettings.announcements);
      console.log('- Font size:', accessibilitySettings.fontSize);
    };
    
    // Initial notification with accessibility considerations
    const modeText = supportsElementCapture ? 
      '🚀 Element Capture Ready!' : 
      '⚠️ Legacy Mode. Use H key to hide panel.';
      
    const audioText = audioSettings.enabled ? 
      ` Audio: ${audioSettings.source}` : '';
      
    const accessibilityText = accessibilitySettings.enabled ?
      ' ♿ Accessibility enabled' : '';
      
    const message = `🎯 Screen Area Recorder Ready!${audioText}${accessibilityText}\n• Select area or Ctrl+A for full page\n${modeText}`;
    
    showNotification(message, 'info');
    
    // Announce initialization for screen readers
    setTimeout(() => {
      announceToScreenReader('Screen Area Recorder Pro initialized and ready. Press H for help or use hotkeys to start recording.');
    }, 1000);
    
  }).catch(error => {
    console.error('[SAR] Initialization error:', error);
    try {
      showNotification('Initialization error - some features may not work properly', 'error');
    } catch (e) {
      console.error('[SAR] Failed to show error notification:', e);
    }
  });

})();