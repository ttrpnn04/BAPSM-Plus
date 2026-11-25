(function () {
  'use strict';

  var titles = {
    success: 'สำเร็จ',
    error: 'เกิดข้อผิดพลาด',
    warning: 'ยืนยันการดำเนินการ',
    info: 'แจ้งเตือน'
  };

  function fire(type, message, options) {
    if (!window.Swal) {
      console.error('ไม่สามารถโหลด SweetAlert2 ได้');
      window.alert((titles[type] || titles.info) + ': ' + (message || 'กรุณาลองใหม่'));
      return Promise.resolve({ isConfirmed: true });
    }

    options = options || {};
    return window.Swal.fire({
      icon: type,
      title: options.title || titles[type] || titles.info,
      text: message || '',
      confirmButtonText: 'ตกลง',
      showConfirmButton: type !== 'success',
      timer: type === 'success' ? 1800 : undefined,
      timerProgressBar: type === 'success',
      showCancelButton: false,
      heightAuto: false,
      customClass: {
        container: 'bap-swal-container',
        popup: 'bap-swal-popup',
        confirmButton: 'bap-swal-confirm',
        cancelButton: 'bap-swal-cancel'
      },
      buttonsStyling: false
    });
  }

  function messageFromHtml(html, fallback) {
    if (!html) return fallback;

    var doc = new DOMParser().parseFromString(html, 'text/html');
    var selectors = [
      '.validation-summary-errors',
      '.field-validation-error',
      '.alert-danger',
      '[role="alert"]'
    ];

    for (var i = 0; i < selectors.length; i += 1) {
      var nodes = Array.prototype.slice.call(doc.querySelectorAll(selectors[i]));
      var messages = nodes
        .map(function (node) { return (node.textContent || '').replace(/\s+/g, ' ').trim(); })
        .filter(Boolean);
      if (messages.length) return Array.from(new Set(messages)).join(' ');
    }

    return fallback;
  }

  function messageFromPayload(payload, fallback) {
    if (!payload || typeof payload !== 'object') return fallback;
    if (typeof payload.message === 'string' && payload.message.trim()) return payload.message;
    if (typeof payload.error === 'string' && payload.error.trim()) return payload.error;
    if (typeof payload.title === 'string' && payload.title.trim()) return payload.title;

    if (payload.errors && typeof payload.errors === 'object') {
      var errors = Object.keys(payload.errors).reduce(function (all, key) {
        var value = payload.errors[key];
        return all.concat(Array.isArray(value) ? value : [value]);
      }, []).filter(function (value) { return typeof value === 'string' && value.trim(); });
      if (errors.length) return Array.from(new Set(errors)).join(' ');
    }

    return fallback;
  }

  function confirm(message, options) {
    options = options || {};
    if (!window.Swal) {
      console.error('ไม่สามารถโหลด SweetAlert2 ได้');
      return Promise.resolve({
        isConfirmed: window.confirm(message || 'ต้องการดำเนินการต่อหรือไม่?')
      });
    }

    return window.Swal.fire({
      icon: options.icon || 'warning',
      title: options.title || titles.warning,
      text: message || 'ต้องการดำเนินการต่อหรือไม่?',
      showCancelButton: true,
      confirmButtonText: options.confirmButtonText || 'ยืนยัน',
      cancelButtonText: 'ยกเลิก',
      focusCancel: true,
      heightAuto: false,
      customClass: {
        container: 'bap-swal-container',
        popup: 'bap-swal-popup',
        confirmButton: 'bap-swal-confirm',
        cancelButton: 'bap-swal-cancel'
      },
      buttonsStyling: false
    });
  }

  window.BapAlerts = {
    success: function (message) { return fire('success', message); },
    error: function (message) { return fire('error', message); },
    warning: function (message) { return fire('warning', message); },
    info: function (message) { return fire('info', message); },
    confirm: confirm,
    messageFromHtml: messageFromHtml,
    messageFromPayload: messageFromPayload,
    errorFromResponse: async function (response, fallback) {
      var defaultMessage = fallback || 'ไม่สามารถดำเนินรายการได้ กรุณาลองใหม่';
      var body = '';
      try {
        body = await response.clone().text();
      } catch (error) {
        console.error('อ่านรายละเอียดข้อผิดพลาดจาก Server ไม่สำเร็จ', error);
      }

      var payload = null;
      try {
        payload = JSON.parse(body);
      } catch (_) {
        // The response may be an MVC validation page rather than JSON.
      }

      var message = payload
        ? messageFromPayload(payload, defaultMessage)
        : messageFromHtml(body, defaultMessage);
      return fire('error', message);
    }
  };

  var notificationQueue = Promise.resolve();
  document.querySelectorAll('[data-bap-notification]').forEach(function (element) {
    var type = element.getAttribute('data-type') || 'info';
    var message = element.getAttribute('data-message') || '';
    var show = window.BapAlerts[type];
    if (typeof show === 'function') {
      notificationQueue = notificationQueue.then(function () {
        return show(message);
      });
    }
  });

  document.addEventListener('submit', function (event) {
    if (event.defaultPrevented) return;

    var form = event.target;
    if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-bap-confirm')) return;

    if (form.dataset.bapConfirmBypass === 'true') {
      delete form.dataset.bapConfirmBypass;
      return;
    }

    event.preventDefault();
    confirm(form.dataset.bapConfirm || 'ต้องการดำเนินการต่อหรือไม่?').then(function (result) {
      if (!result.isConfirmed || !form.isConnected) return;
      form.dataset.bapConfirmBypass = 'true';
      if (typeof form.requestSubmit === 'function') {
        form.requestSubmit();
      } else {
        HTMLFormElement.prototype.submit.call(form);
      }
    });
  });
})();
