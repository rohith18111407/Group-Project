import { Injectable } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import * as signalR from '@microsoft/signalr';

export interface Notification {
  id: string;
  action: string;
  message: string;
  timestamp: Date;
  type: 'info' | 'success' | 'warning' | 'error';
  details?: any;
  isRead: boolean;
  
  // Properties expected by the right sidebar
  fileName?: string;
  fileExtension?: string;
  itemName?: string;
  itemCategoryLabel?: string;
  fileCategoryLabel?: string;
  versionNumber?: number;
  createdAt?: Date;
  createdBy?: string;
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private hubConnection: signalR.HubConnection | null = null;
  private notificationsSubject = new BehaviorSubject<Notification[]>([]);
  private connectionStateSubject = new BehaviorSubject<boolean>(false);
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;
  private isManuallyDisconnected = false; // Flag to prevent reconnection after logout

  public notifications$ = this.notificationsSubject.asObservable();
  public connectionState$ = this.connectionStateSubject.asObservable();
  
  public unreadCount$: Observable<number> = this.notifications$.pipe(
    map(notifications => notifications.filter(n => !n.isRead).length)
  );

  constructor() {
    // Initialize connection when service is created (typically when user is already logged in)
    this.initialize();
  }

  private initializeConnection(): void {
    // Don't initialize if manually disconnected (e.g., after logout)
    if (this.isManuallyDisconnected) {
      console.log('SignalR connection initialization skipped - manually disconnected');
      return;
    }

    const token = sessionStorage.getItem('jwtToken');
    
    if (!token) {
      console.warn('No JWT token found, cannot initialize SignalR connection');
      return;
    }

    // Validate token hasn't expired
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const currentTime = Math.floor(Date.now() / 1000);
      if (payload.exp && payload.exp < currentTime) {
        console.warn('JWT token has expired, cannot initialize SignalR connection');
        return;
      }
    } catch (error) {
      console.warn('Invalid JWT token format, cannot initialize SignalR connection');
      return;
    }

    console.log('Initializing SignalR connection...');

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5239/notificationhub', {
        accessTokenFactory: () => {
          const currentToken = sessionStorage.getItem('jwtToken');
          console.log('Providing token for SignalR:', currentToken ? 'Token found' : 'No token');
          return currentToken || '';
        },
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents | signalR.HttpTransportType.LongPolling,
        skipNegotiation: false
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
          if (retryContext.previousRetryCount < 4) {
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          } else {
            return null; // Stop retrying
          }
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    this.startConnection();
    this.setupConnectionEvents();
  }

  private async startConnection(): Promise<void> {
    if (!this.hubConnection) return;

    try {
      await this.hubConnection.start();
      console.log('✅ SignalR connection started successfully');
      this.connectionStateSubject.next(true);
      this.reconnectAttempts = 0;
      this.setupEventHandlers();
    } catch (error: any) {
      console.error('❌ Error starting SignalR connection:', error);
      this.connectionStateSubject.next(false);
      
      // Manual reconnection with exponential backoff
      this.handleReconnection();
    }
  }

  private setupConnectionEvents(): void {
    if (!this.hubConnection) return;

    this.hubConnection.onclose((error) => {
      console.log('SignalR connection closed:', error?.message || 'No error message');
      this.connectionStateSubject.next(false);
      
      // Only attempt reconnection if not manually disconnected
      if (!this.isManuallyDisconnected) {
        this.handleReconnection();
      }
    });

    this.hubConnection.onreconnecting((error) => {
      console.log('SignalR reconnecting...', error?.message || 'No error message');
      this.connectionStateSubject.next(false);
    });

    this.hubConnection.onreconnected((connectionId) => {
      console.log('SignalR reconnected with connectionId:', connectionId);
      this.connectionStateSubject.next(true);
      this.reconnectAttempts = 0;
    });
  }

  private handleReconnection(): void {
    // Don't attempt reconnection if manually disconnected or no token available
    if (this.isManuallyDisconnected) {
      console.log('Reconnection skipped - manually disconnected');
      return;
    }

    const token = sessionStorage.getItem('jwtToken');
    if (!token) {
      console.log('Reconnection skipped - no JWT token available');
      return;
    }

    // Validate token hasn't expired
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const currentTime = Math.floor(Date.now() / 1000);
      if (payload.exp && payload.exp < currentTime) {
        console.log('Reconnection skipped - JWT token expired');
        return;
      }
    } catch (error) {
      console.log('Reconnection skipped - invalid JWT token');
      return;
    }

    if (this.reconnectAttempts >= this.maxReconnectAttempts) {
      console.error('Max reconnection attempts reached. Please refresh the page.');
      return;
    }

    const delay = Math.min(1000 * Math.pow(2, this.reconnectAttempts), 30000);
    this.reconnectAttempts++;
    
    console.log(`Attempting to reconnect in ${delay}ms... (Attempt ${this.reconnectAttempts}/${this.maxReconnectAttempts})`);
    
    setTimeout(() => {
      if (this.hubConnection?.state === signalR.HubConnectionState.Disconnected && !this.isManuallyDisconnected) {
        this.startConnection();
      }
    }, delay);
  }

  private setupEventHandlers(): void {
    if (!this.hubConnection) return;

    console.log('Setting up SignalR event handlers...');

    this.hubConnection.on('ReceiveNotification', (data: any) => {
      console.log('🔔 Received notification from SignalR:', data);
      this.handleNotification(data);
    });

    this.hubConnection.on('TestConnection', (message: string) => {
      console.log('✅ Test connection message:', message);
    });

    console.log('SignalR event handlers configured');
  }

  private handleNotification(data: any): void {
    console.log('Processing notification:', data);
    
    // Handle both uppercase and lowercase property names from backend
    const action = data.Action || data.action || 'Unknown Action';
    console.log('Extracted action:', action);
    console.log('Available properties:', Object.keys(data));
    
    const notification: Notification = {
      id: this.generateId(),
      action: action,
      message: this.formatNotificationMessage(data),
      timestamp: new Date(),
      type: this.getNotificationType(action),
      details: data,
      isRead: false,
      
      // Map backend data to right sidebar expected format (handle both cases)
      fileName: data.FileName || data.fileName || data.Files?.[0]?.FileName,
      fileExtension: data.FileExtension || data.fileExtension || data.Files?.[0]?.FileExtension,
      itemName: data.ItemName || data.itemName || data.Files?.[0]?.ItemName,
      itemCategoryLabel: data.ItemCategory || data.itemCategory || data.Files?.[0]?.ItemCategory,
      fileCategoryLabel: data.FileCategory || data.fileCategory || data.Files?.[0]?.Category,
      versionNumber: data.VersionNumber || data.versionNumber || data.Files?.[0]?.VersionNumber,
      createdAt: data.CreatedAt || data.createdAt || data.ArchivedAt || data.archivedAt ? 
        new Date(data.CreatedAt || data.createdAt || data.ArchivedAt || data.archivedAt) : new Date(),
      createdBy: data.CreatedBy || data.createdBy || data.ArchivedBy || data.archivedBy || 
                 data.UnarchivedBy || data.unarchivedBy || data.TriggeredBy || data.triggeredBy
    };

    console.log('Processed notification:', notification);

    const currentNotifications = this.notificationsSubject.value;
    const updatedNotifications = [notification, ...currentNotifications];
    
    console.log('Updating notifications. Total count:', updatedNotifications.length);
    this.notificationsSubject.next(updatedNotifications);

    // Show browser notification if supported
    this.showBrowserNotification(notification);
  }

  private formatNotificationMessage(data: any): string {
    // Handle both uppercase and lowercase property names
    const action = data.Action || data.action || 'Unknown Action';
    const fileName = data.FileName || data.fileName || 'Unknown';
    const fileExtension = data.FileExtension || data.fileExtension || '';
    const filesArchivedCount = data.FilesArchivedCount || data.filesArchivedCount || 0;
    const inactiveAdminsCount = data.InactiveAdminsCount || data.inactiveAdminsCount || 0;
    const unarchivedBy = data.UnarchivedBy || data.unarchivedBy || 'Unknown';
    const archivedBy = data.ArchivedBy || data.archivedBy || 'Unknown';
    const triggeredBy = data.TriggeredBy || data.triggeredBy || 'Unknown';
    const inactiveDaysThreshold = data.InactiveDaysThreshold || data.inactiveDaysThreshold || 30;
    const message = data.Message || data.message || '';

    switch (action) {
      case 'Files Archived Due to Admin Inactivity':
        return `${filesArchivedCount} files archived from ${inactiveAdminsCount} inactive admins`;
      
      case 'File Unarchived':
        return `File "${fileName}${fileExtension}" was unarchived by ${unarchivedBy}`;
      
      case 'File Manually Archived':
        return `File "${fileName}${fileExtension}" was archived by ${archivedBy}`;
      
      case 'Manual Inactive Admin Archival Triggered':
        return `Manual archival triggered: ${filesArchivedCount} files archived from ${inactiveAdminsCount} inactive admins`;
      
      case 'General Inactivity Archival Triggered':
        return `General inactivity archival: ${filesArchivedCount} files archived due to ${inactiveDaysThreshold} days of inactivity`;
      
      case 'Test Notification':
        return message || 'Test notification received';
      
      default:
        return message || `${action}`;
    }
  }

  private getNotificationType(action: string): 'info' | 'success' | 'warning' | 'error' {
    // Normalize action to handle both uppercase and lowercase
    const normalizedAction = action.toLowerCase();
    
    if (normalizedAction.includes('archived due to') || 
        normalizedAction.includes('archival triggered') || 
        normalizedAction.includes('inactivity')) {
      return 'warning';
    }
    
    if (normalizedAction.includes('unarchived')) {
      return 'success';
    }
    
    if (normalizedAction.includes('manually archived')) {
      return 'info';
    }
    
    if (normalizedAction.includes('test')) {
      return 'info';
    }
    
    return 'info';
  }

  private generateId(): string {
    return Date.now().toString() + Math.random().toString(36).substr(2, 9);
  }

  private showBrowserNotification(notification: Notification): void {
    if ('Notification' in window && Notification.permission === 'granted') {
      new Notification('File Archive System', {
        body: notification.message,
        icon: '/assets/icons/archive-icon.png'
      });
    }
  }

  public markAsRead(notificationId: string): void {
    const notifications = this.notificationsSubject.value;
    const updatedNotifications = notifications.map(n => 
      n.id === notificationId ? { ...n, isRead: true } : n
    );
    this.notificationsSubject.next(updatedNotifications);
  }

  public markAllAsRead(): void {
    const notifications = this.notificationsSubject.value;
    const updatedNotifications = notifications.map(n => ({ ...n, isRead: true }));
    this.notificationsSubject.next(updatedNotifications);
  }

  public clearNotification(notificationId: string): void {
    const notifications = this.notificationsSubject.value;
    const updatedNotifications = notifications.filter(n => n.id !== notificationId);
    this.notificationsSubject.next(updatedNotifications);
  }

  public clearAllNotifications(): void {
    this.notificationsSubject.next([]);
  }

  public getUnreadCount(): Observable<number> {
    return this.unreadCount$;
  }

  public requestNotificationPermission(): void {
    if ('Notification' in window && Notification.permission === 'default') {
      Notification.requestPermission().then(permission => {
        console.log('Notification permission:', permission);
      });
    }
  }

  public async disconnect(): Promise<void> {
    console.log('Disconnecting SignalR connection...');
    
    // Set flag to prevent reconnection attempts
    this.isManuallyDisconnected = true;
    this.connectionStateSubject.next(false);

    if (this.hubConnection) {
      try {
        // Stop the connection gracefully
        await this.hubConnection.stop();
        console.log('SignalR connection stopped successfully');
      } catch (error) {
        console.error('Error stopping SignalR connection:', error);
      } finally {
        this.hubConnection = null;
      }
    }
  }

  public async reconnect(): Promise<void> {
    console.log('Manually reconnecting SignalR...');
    
    // Reset the manual disconnect flag
    this.isManuallyDisconnected = false;
    this.reconnectAttempts = 0;
    
    // Disconnect first if there's an existing connection
    if (this.hubConnection) {
      try {
        await this.hubConnection.stop();
      } catch (error) {
        console.warn('Error stopping existing connection during reconnect:', error);
      }
      this.hubConnection = null;
    }
    
    // Initialize new connection
    this.initializeConnection();
  }

  // Test method to send a test notification from the client
  public async sendTestNotification(): Promise<void> {
    if (this.hubConnection && this.hubConnection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.hubConnection.invoke('SendTestNotification');
        console.log('Test notification sent to server');
      } catch (error) {
        console.error('Error sending test notification:', error);
      }
    } else {
      console.warn('Cannot send test notification: Not connected to SignalR hub');
    }
  }

  public initialize(): void {
    console.log('Initializing notification service...');
    this.isManuallyDisconnected = false;
    this.reconnectAttempts = 0;
    this.initializeConnection();
  }

  // Method to call when user logs in
  public onUserLogin(): void {
    console.log('User logged in - initializing SignalR connection');
    this.initialize();
  }

  // Method to call when user logs out
  public async onUserLogout(): Promise<void> {
    console.log('User logging out - disconnecting SignalR connection');
    await this.disconnect();
  }

  ngOnDestroy(): void {
    this.disconnect().catch(error => {
      console.error('Error during service cleanup:', error);
    });
  }

}