import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { BehaviorSubject } from 'rxjs';
import { FileNotification } from '../models/filenotification.model';
import { CategoryTypeLabels } from '../models/category-type.enum';

@Injectable({ providedIn: 'root' })
export class NotificationService {
    private hubConnection: signalR.HubConnection;
    private notificationsSubject = new BehaviorSubject<any[]>([]);
    private unreadCountSubject = new BehaviorSubject<number>(0);

    notifications$ = this.notificationsSubject.asObservable();
    unreadCount$ = this.unreadCountSubject.asObservable();

    constructor() {
        this.hubConnection = new signalR.HubConnectionBuilder()
            .withUrl('http://localhost:5239/notificationhub')
            .withAutomaticReconnect()
            .build();

        this.hubConnection.start().catch((err: any) => console.error('SignalR Connection Error:', err));

        this.hubConnection.on('ReceiveNotification', (data: FileNotification) => {
            const current = this.notificationsSubject.value;

            const fileCategory = Number(data.category);

            const enrichedData = {
                ...data,
                fileCategoryLabel: CategoryTypeLabels[data.category] || 'Unknown',
                itemCategoryLabel: data.itemCategory || 'Unknown'  // <-- itemCategory must be in payload
            };

            this.notificationsSubject.next([enrichedData, ...current]);
            this.unreadCountSubject.next(this.unreadCountSubject.value + 1);
        });


    }

    markAllAsRead() {
        this.unreadCountSubject.next(0);
    }

    clearAllNotifications() {
        this.notificationsSubject.next([]);
        this.unreadCountSubject.next(0);
    }
}
