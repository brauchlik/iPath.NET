// iPath.NET — Interactive FHIR Questionnaire Review Studio Controller
(function() {
  'use strict';

  // State
  let allItems = [];
  let currentItem = null;
  let showDeprecated = false;
  let searchQuery = '';
  let activeTab = 'tab-form';
  let isLFormsReady = false;

  // DOM Elements
  const sidebarList = document.getElementById('sidebar-list');
  const itemCountBadge = document.getElementById('item-count');
  const searchInput = document.getElementById('search-input');
  const toggleDeprecatedCheckbox = document.getElementById('toggle-deprecated');

  const itemTitle = document.getElementById('item-title');
  const itemIdPill = document.getElementById('item-id');
  const itemBadges = document.getElementById('item-badges');
  const itemSummary = document.getElementById('item-summary');
  const openQuestionsBox = document.getElementById('open-questions-box');
  const openQuestionsList = document.getElementById('open-questions-list');

  const formContainer = document.getElementById('form-container');
  const notesContent = document.getElementById('notes-content');
  const originalContent = document.getElementById('original-content');
  const fhirJsonCode = document.getElementById('fhir-json-code');

  const notesFilePath = document.getElementById('notes-file-path');
  const originalFilePath = document.getElementById('original-file-path');
  const fhirFilePath = document.getElementById('fhir-file-path');

  const btnResetForm = document.getElementById('btn-reset-form');
  const btnGetResponse = document.getElementById('btn-get-response');
  const btnCopyFhir = document.getElementById('btn-copy-fhir');
  const tabButtons = document.querySelectorAll('.tab-btn');

  const responseDialog = document.getElementById('response-dialog');
  const responseJsonCode = document.getElementById('response-json-code');
  const btnCloseDialog = document.getElementById('btn-close-dialog');
  const btnCopyResponse = document.getElementById('btn-copy-response');

  // Simple Markdown Fallback parser in case Marked is blocked offline
  function renderMarkdown(mdText) {
    if (!mdText) return '<p class="placeholder-msg">No content available.</p>';
    if (typeof marked !== 'undefined' && marked.parse) {
      return marked.parse(mdText);
    }
    let html = mdText
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/^### (.*$)/gim, '<h3>$1</h3>')
      .replace(/^## (.*$)/gim, '<h2>$1</h2>')
      .replace(/^# (.*$)/gim, '<h1>$1</h1>')
      .replace(/^\- \[(x| )\] (.*$)/gim, (m, checked, text) => {
        return `<li><input type="checkbox" disabled ${checked === 'x' ? 'checked' : ''}> ${text}</li>`;
      })
      .replace(/^\- (.*$)/gim, '<li>$1</li>')
      .replace(/`([^`]+)`/g, '<code>$1</code>')
      .replace(/\n\n/g, '<br><br>');
    return html;
  }

  // Polling helper: Wait for LHC-Forms scripts to finish downloading & initializing
  function whenLFormsReady(onSuccess, onError) {
    if (isLFormsReady || (typeof LForms !== 'undefined' && LForms.Util && LForms.Util.addFormToPage)) {
      isLFormsReady = true;
      onSuccess();
      return;
    }

    let attempts = 0;
    const maxAttempts = 100; // 10 seconds max (100 * 100ms)
    const interval = setInterval(() => {
      attempts++;
      if (typeof LForms !== 'undefined' && LForms.Util && LForms.Util.addFormToPage) {
        clearInterval(interval);
        isLFormsReady = true;
        onSuccess();
      } else if (attempts >= maxAttempts) {
        clearInterval(interval);
        if (onError) onError();
      }
    }, 100);
  }

  // Initialize Data
  function initData() {
    const data = window.QUESTIONNAIRE_REGISTRY_DATA || { buildingBlocks: [], caseDescriptions: [] };
    const blocks = (data.buildingBlocks || []).map(b => ({ ...b, itemType: 'block' }));
    const cases = (data.caseDescriptions || []).map(c => ({ ...c, itemType: 'case' }));
    allItems = [...blocks, ...cases];
  }

  // Filter Items
  function getFilteredItems() {
    return allItems.filter(item => {
      if (!showDeprecated && item.deprecated) {
        return false;
      }
      if (searchQuery) {
        const q = searchQuery.toLowerCase();
        const titleMatch = (item.title || '').toLowerCase().includes(q);
        const idMatch = (item.id || '').toLowerCase().includes(q);
        const catMatch = (item.category || '').toLowerCase().includes(q);
        const topoMatch = (item.topography || []).some(t => t.toLowerCase().includes(q));
        const summaryMatch = (item.summary || '').toLowerCase().includes(q);
        if (!titleMatch && !idMatch && !catMatch && !topoMatch && !summaryMatch) {
          return false;
        }
      }
      return true;
    });
  }

  // Render Sidebar
  function renderSidebar() {
    const filtered = getFilteredItems();
    itemCountBadge.textContent = filtered.length;
    sidebarList.innerHTML = '';

    if (filtered.length === 0) {
      sidebarList.innerHTML = '<div style="padding: 1rem; color: #94a3b8; font-size: 0.85rem; text-align: center;">No questionnaires match criteria.</div>';
      return;
    }

    // Group by category
    const groups = {};
    filtered.forEach(item => {
      const cat = item.category || 'Other';
      if (!groups[cat]) groups[cat] = [];
      groups[cat].push(item);
    });

    Object.keys(groups).sort().forEach(catName => {
      const groupEl = document.createElement('div');
      groupEl.className = 'nav-category-group';

      const headerEl = document.createElement('div');
      headerEl.className = 'nav-category-header';
      headerEl.textContent = catName;
      groupEl.appendChild(headerEl);

      groups[catName].forEach(item => {
        const itemEl = document.createElement('div');
        itemEl.className = `nav-item ${item.deprecated ? 'deprecated' : ''} ${currentItem && currentItem.id === item.id ? 'active' : ''}`;
        itemEl.dataset.id = item.id;

        const titleEl = document.createElement('div');
        titleEl.className = 'nav-item-title';
        titleEl.textContent = item.title;
        itemEl.appendChild(titleEl);

        const metaEl = document.createElement('div');
        metaEl.className = 'nav-item-meta';

        if (item.topography && item.topography.length > 0) {
          const topoPill = document.createElement('span');
          topoPill.className = 'pill-topo';
          topoPill.textContent = item.topography.join(', ');
          metaEl.appendChild(topoPill);
        }

        if (item.deprecated) {
          const depPill = document.createElement('span');
          depPill.className = 'pill-deprecated';
          depPill.textContent = item.status || 'deprecated';
          metaEl.appendChild(depPill);
        }

        itemEl.appendChild(metaEl);

        itemEl.addEventListener('click', () => {
          selectItem(item);
        });

        groupEl.appendChild(itemEl);
      });

      sidebarList.appendChild(groupEl);
    });
  }

  // Select and Render Item
  function selectItem(item) {
    currentItem = item;
    renderSidebar(); // refresh active state in sidebar

    // Update Header Card
    itemTitle.textContent = item.title;
    itemIdPill.textContent = item.id;
    itemSummary.textContent = item.summary || '';

    // Badges
    itemBadges.innerHTML = '';
    const statusBadge = document.createElement('span');
    statusBadge.className = `badge ${item.deprecated ? 'badge-superseded' : 'badge-ready'}`;
    statusBadge.textContent = item.status || (item.deprecated ? 'Superseded' : 'Ready for Review');
    itemBadges.appendChild(statusBadge);

    if (item.topography && item.topography.length > 0) {
      const topoBadge = document.createElement('span');
      topoBadge.className = 'badge';
      topoBadge.style.backgroundColor = '#e0f2fe';
      topoBadge.style.color = '#0369a1';
      topoBadge.style.border = '1px solid #bae6fd';
      topoBadge.textContent = `Scope: ${item.topography.join(', ')}`;
      itemBadges.appendChild(topoBadge);
    }

    // Open Questions Box
    if (item.openQuestions && item.openQuestions.length > 0) {
      openQuestionsBox.style.display = 'block';
      openQuestionsList.innerHTML = '';
      item.openQuestions.forEach(q => {
        const li = document.createElement('li');
        li.textContent = q;
        openQuestionsList.appendChild(li);
      });
    } else {
      openQuestionsBox.style.display = 'none';
    }

    // File Paths in Toolbars
    notesFilePath.textContent = `Source: ${item.sourceNotes || 'N/A'}`;
    originalFilePath.textContent = `Original: ${item.sourceOriginal || 'N/A'}`;
    fhirFilePath.textContent = `FHIR JSON: ${item.sourceFhir || 'N/A'}`;

    // Render Working Notes
    notesContent.innerHTML = renderMarkdown(item.notesContent || '*(No companion notes file provided)*');

    // Render Pathologist Original
    originalContent.innerHTML = renderMarkdown(item.originalContent || '*(No original document provided)*');

    // Render FHIR JSON
    const formattedJson = JSON.stringify(item.fhirJson || {}, null, 2);
    fhirJsonCode.textContent = formattedJson;

    // Render Live LHC Form
    renderLhcForm(item.fhirJson);
  }

  // Render LHC Form Widget
  function renderLhcForm(questionnaireJson) {
    formContainer.innerHTML = '';

    if (!questionnaireJson) {
      formContainer.innerHTML = '<div class="placeholder-msg">No FHIR Questionnaire available.</div>';
      return;
    }

    // Show loading state while LHC-Forms is preparing
    formContainer.innerHTML = `
      <div class="placeholder-msg" style="color: #0369a1;">
        <span style="font-size: 1.5rem; display: block; margin-bottom: 0.5rem;">⏳</span>
        Loading LHC-Forms widget...
      </div>
    `;

    whenLFormsReady(
      () => {
        // Ready! Create mount point and add form
        formContainer.innerHTML = '';
        const mountPoint = document.createElement('div');
        mountPoint.id = 'lhc-form-mount';
        formContainer.appendChild(mountPoint);

        try {
          LForms.Util.addFormToPage(questionnaireJson, 'lhc-form-mount', { fhirVersion: 'R4' });
        } catch (err) {
          console.error('Error rendering LHC Forms widget:', err);
          formContainer.innerHTML = `<div class="placeholder-msg" style="color: #dc2626;">Error rendering LHC-Forms widget: ${err.message}</div>`;
        }
      },
      () => {
        // Failed after timeout
        formContainer.innerHTML = `
          <div class="placeholder-msg" style="color: #d97706; padding: 2rem;">
            <p style="font-weight: 600; font-size: 1.05rem;">⚠️ LHC-Forms library is initializing or encountered an issue.</p>
            <p style="font-size: 0.85rem; margin-top: 0.75rem; line-height: 1.5;">
              You can inspect the <strong>Working Notes</strong>, <strong>Pathologist Original</strong>, and <strong>FHIR JSON</strong> tabs above.
            </p>
          </div>
        `;
      }
    );
  }

  // Tab Switching
  function setupTabs() {
    tabButtons.forEach(btn => {
      btn.addEventListener('click', () => {
        const tabId = btn.dataset.tab;
        activeTab = tabId;

        tabButtons.forEach(b => b.classList.toggle('active', b === btn));
        document.querySelectorAll('.tab-pane').forEach(pane => {
          pane.classList.toggle('active', pane.id === tabId);
        });
      });
    });
  }

  // Event Listeners
  function setupEvents() {
    searchInput.addEventListener('input', (e) => {
      searchQuery = e.target.value;
      renderSidebar();
    });

    toggleDeprecatedCheckbox.addEventListener('change', (e) => {
      showDeprecated = e.target.checked;
      renderSidebar();
    });

    btnResetForm.addEventListener('click', () => {
      if (currentItem && currentItem.fhirJson) {
        renderLhcForm(currentItem.fhirJson);
      }
    });

    btnGetResponse.addEventListener('click', () => {
      if (typeof LForms !== 'undefined' && LForms.Util && LForms.Util.getFormFHIRData) {
        try {
          const response = LForms.Util.getFormFHIRData('QuestionnaireResponse', 'R4', 'lhc-form-mount');
          if (response) {
            responseJsonCode.textContent = JSON.stringify(response, null, 2);
            responseDialog.showModal();
          } else {
            alert('No response data extracted yet. Try answering some questions first.');
          }
        } catch (err) {
          alert('Could not extract QuestionnaireResponse: ' + err.message);
        }
      } else {
        alert('LHC-Forms is not ready yet.');
      }
    });

    btnCloseDialog.addEventListener('click', () => {
      responseDialog.close();
    });

    btnCopyResponse.addEventListener('click', () => {
      navigator.clipboard.writeText(responseJsonCode.textContent).then(() => {
        const originalText = btnCopyResponse.textContent;
        btnCopyResponse.textContent = '✅ Copied!';
        setTimeout(() => { btnCopyResponse.textContent = originalText; }, 2000);
      });
    });

    btnCopyFhir.addEventListener('click', () => {
      if (currentItem && currentItem.fhirJson) {
        const text = JSON.stringify(currentItem.fhirJson, null, 2);
        navigator.clipboard.writeText(text).then(() => {
          const originalText = btnCopyFhir.textContent;
          btnCopyFhir.textContent = '✅ Copied!';
          setTimeout(() => { btnCopyFhir.textContent = originalText; }, 2000);
        }).catch(err => {
          alert('Could not copy to clipboard: ' + err);
        });
      }
    });
  }

  // Bootstrap
  function init() {
    initData();
    setupTabs();
    setupEvents();
    renderSidebar();

    // Select first non-deprecated item by default
    const filtered = getFilteredItems();
    if (filtered.length > 0) {
      selectItem(filtered[0]);
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
