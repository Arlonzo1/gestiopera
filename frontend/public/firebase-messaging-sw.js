/* Firebase web messaging worker. Values are replaced by the deployment build. */
importScripts('https://www.gstatic.com/firebasejs/11.0.2/firebase-app-compat.js')
importScripts('https://www.gstatic.com/firebasejs/11.0.2/firebase-messaging-compat.js')

const config = new URLSearchParams(self.location.search)
firebase.initializeApp({
  apiKey: config.get('apiKey') || '',
  authDomain: config.get('authDomain') || '',
  projectId: config.get('projectId') || '',
  storageBucket: config.get('storageBucket') || '',
  messagingSenderId: config.get('messagingSenderId') || '',
  appId: config.get('appId') || ''
})

const messaging = firebase.messaging()
messaging.onBackgroundMessage((payload) => {
  const notification = payload.notification || {}
  self.registration.showNotification(notification.title || 'Billares POS', {
    body: notification.body || 'Tienes una nueva notificación.',
    data: payload.data || {}
  })
})
