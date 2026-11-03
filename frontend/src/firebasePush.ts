import { FirebaseApp, getApp, getApps, initializeApp } from 'firebase/app'
import { getMessaging, getToken, isSupported, Messaging, onMessage, Unsubscribe } from 'firebase/messaging'

const firebaseConfig = {
  apiKey: import.meta.env.VITE_FIREBASE_API_KEY,
  authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN,
  projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID,
  storageBucket: import.meta.env.VITE_FIREBASE_STORAGE_BUCKET,
  messagingSenderId: import.meta.env.VITE_FIREBASE_MESSAGING_SENDER_ID,
  appId: import.meta.env.VITE_FIREBASE_APP_ID
}

const vapidKey = import.meta.env.VITE_FIREBASE_VAPID_KEY as string | undefined

function isConfigured() {
  return Boolean(firebaseConfig.apiKey && firebaseConfig.projectId && firebaseConfig.messagingSenderId && firebaseConfig.appId && vapidKey)
}

function getFirebaseMessaging(): Messaging | null {
  if (!isConfigured()) return null
  const app: FirebaseApp = getApps().length > 0 ? getApp() : initializeApp(firebaseConfig)
  return getMessaging(app)
}

export async function requestPushToken(): Promise<string | null> {
  if (!isConfigured() || !('serviceWorker' in navigator) || !('Notification' in window)) return null
  if (!(await isSupported())) return null

  const permission = await Notification.requestPermission()
  if (permission !== 'granted') return null

  const workerConfig = new URLSearchParams(Object.entries(firebaseConfig).map(([key, value]) => [key, String(value)]))
  const registration = await navigator.serviceWorker.register(`/firebase-messaging-sw.js?${workerConfig.toString()}`)
  const messaging = getFirebaseMessaging()
  if (!messaging) return null
  return getToken(messaging, { vapidKey, serviceWorkerRegistration: registration })
}

export function isPushConfigured() {
  return isConfigured()
}

export async function listenToForegroundMessages(onNotification: (title: string, body: string) => void): Promise<Unsubscribe | null> {
  if (!isConfigured() || !('Notification' in window) || Notification.permission !== 'granted') return null
  if (!(await isSupported())) return null

  const messaging = getFirebaseMessaging()
  if (!messaging) return null

  return onMessage(messaging, payload => {
    const title = payload.notification?.title ?? payload.data?.title
    const body = payload.notification?.body ?? payload.data?.body ?? ''
    if (title) onNotification(title, body)
  })
}