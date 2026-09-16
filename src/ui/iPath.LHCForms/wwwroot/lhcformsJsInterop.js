// LForms is loaded as plain <script> tags in App.razor, so it may not be ready when
// Blazor renders - especially under WebAssembly over a slow link, where OnAfterRender
// can beat the CDN. Calling addFormToPage too early throws, and the form silently
// stays blank or loads without FHIR support (skip logic then never fires).
//
// The FHIR check matters as much as the Util check: lhc-forms.js defines LForms.Util,
// but it is lformsFHIR.min.js that adds LForms.FHIR.R4, and addFormToPage needs both.

const READY_TIMEOUT_MS = 10000;
const POLL_INTERVAL_MS = 100;

function isReady() {
    return typeof LForms !== 'undefined'
        && LForms.Util
        && typeof LForms.Util.addFormToPage === 'function'
        && LForms.FHIR
        && LForms.FHIR.R4;
}

function whenReady() {
    if (isReady()) return Promise.resolve();

    return new Promise((resolve, reject) => {
        const deadline = Date.now() + READY_TIMEOUT_MS;
        const timer = setInterval(() => {
            if (isReady()) {
                clearInterval(timer);
                resolve();
            } else if (Date.now() > deadline) {
                clearInterval(timer);
                reject(new Error('LForms did not finish loading. Check that the LHC-Forms scripts are reachable.'));
            }
        }, POLL_INTERVAL_MS);
    });
}

// LForms' internal _codingsEqual() dereferences both arguments (.system/.code/.text)
// without a null check. While a multi-select answer with sub-items settles, the value
// array can transiently hold a null, so skip-logic evaluation throws
// "Cannot read properties of null (reading 'system')" and aborts - leaving the
// dependent sub-items unexpanded. Verified still unguarded in LForms 44.0.0.
// A null answer equals no trigger, so returning false is the correct semantics; the
// next real update re-evaluates normally.
function guardCodingComparison(componentId, attemptsLeft = 20) {
    const host = document.getElementById(componentId);
    const form = host && host.getElementsByTagName('wc-lhc-form')[0]?.lhcFormData;

    if (!form) {
        if (attemptsLeft > 0) {
            setTimeout(() => guardCodingComparison(componentId, attemptsLeft - 1), 100);
        }
        return;
    }

    const owner = Object.prototype.hasOwnProperty.call(form, '_codingsEqual')
        ? form
        : Object.getPrototypeOf(form);

    if (!owner || typeof owner._codingsEqual !== 'function' || owner.__ipathCodingGuard) return;

    const original = owner._codingsEqual;
    owner._codingsEqual = function (a, b) {
        if (a == null || b == null) return false;
        return original.call(this, a, b);
    };
    owner.__ipathCodingGuard = true;
}

// Load a Questionnaire, optionally with an existing QuestionnaireResponse.
export async function loadData(questionnaireJson, responseJson, componentId, asReadonly) {
    await whenReady();

    const qData = JSON.parse(questionnaireJson);
    const qrData = responseJson ? JSON.parse(responseJson) : null;

    await LForms.Util.addFormToPage(qData, componentId, {
        fhirVersion: 'R4',
        questionnaireResponse: qrData,
        readonlyMode: asReadonly
    });

    guardCodingComparison(componentId);
}

// Read what the user entered back out as a QuestionnaireResponse.
export function getData(componentId, options) {
    if (!isReady()) throw new Error('LForms is not loaded.');
    const response = LForms.Util.getFormFHIRData('QuestionnaireResponse', 'R4', componentId, options);
    return JSON.stringify(response, null, 2);
}
