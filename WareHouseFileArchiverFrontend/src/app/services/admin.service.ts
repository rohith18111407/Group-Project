import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { map, Observable } from "rxjs";

@Injectable({ providedIn: 'root' })
export class AdminService {
    private readonly baseUrl = 'http://localhost:5239/api/v1';

    constructor(private http: HttpClient) { }

    private getAuthHeaders(): HttpHeaders {
        const token = sessionStorage.getItem('jwtToken');
        return new HttpHeaders({ 'Authorization': `Bearer ${token}` });
    }

    getUserProfile(): Observable<any> {
        return this.http.get<any>(`${this.baseUrl}/auth/me`, { headers: this.getAuthHeaders() }).pipe(
            map(res => res.data)
        );
    }

    getAllItems(sortBy: 'createdAt' | 'createdBy' | 'name' | 'category' = 'createdAt', isDescending: boolean = false): Observable<any[]> {
        const params = new URLSearchParams({
            isDescending: isDescending.toString(),
            pageNumber: '1',
            pageSize: '100',
            sortBy
        });

        return this.http.get<any>(`${this.baseUrl}/Items?${params.toString()}`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    createItem(item: any): Observable<any> {
        return this.http.post(`${this.baseUrl}/items`, item, { headers: this.getAuthHeaders() });
    }

    deleteItem(id: string): Observable<any> {
        return this.http.delete(`${this.baseUrl}/items/${id}`, { headers: this.getAuthHeaders() });
    }

    updateItem(id: string, payload: { name: string; description: string; categories: string[] }): Observable<any> {
        return this.http.put(`${this.baseUrl}/items/${id}`, payload, { headers: this.getAuthHeaders() });
    }

    getAllUsers(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/Users?pageNumber=1&pageSize=100`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    createUser(user: { username: string; password: string; roles: string[] }): Observable<any> {
        return this.http.post(`${this.baseUrl}/users`, user, { headers: this.getAuthHeaders() });
    }

    deleteUser(id: string): Observable<any> {
        return this.http.delete(`${this.baseUrl}/Users/${id}`, { headers: this.getAuthHeaders() });
    }

    updateUser(id: string, payload: { username: string; roles: string[] }): Observable<any> {
        return this.http.put(`${this.baseUrl}/users/${id}`, payload, { headers: this.getAuthHeaders() });
    }

    getAllFiles(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/files`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    uploadFile(fileData: FormData): Observable<any> {
        return this.http.post(`${this.baseUrl}/files/upload`, fileData, { headers: this.getAuthHeaders() });
    }

    deleteFile(id: string) {
        return this.http.delete<any>(`http://localhost:5239/api/v1/files/${id}`, { headers: this.getAuthHeaders() });
    }

    downloadFile(filename: string, version: number) {
        const url = `${this.baseUrl}/files/${filename}/v${version}`;
        return this.http.get(url, { headers: this.getAuthHeaders(), responseType: 'blob' });
    }

    bulkDownloadFiles(fileIds: string[], zipFileName?: string): Observable<Blob> {
        const request = {
            fileIds: fileIds,
            zipFileName: zipFileName || `ArchiveFiles_${new Date().toISOString().slice(0, 19).replace(/:/g, '-')}`
        };

        return this.http.post(`${this.baseUrl}/files/bulk-download`, request, {
            headers: this.getAuthHeaders(),
            responseType: 'blob'
        });
    }

    // New methods for scheduled uploads
    getScheduledFiles(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/files/scheduled`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    cancelScheduledUpload(id: string): Observable<any> {
        return this.http.delete(`${this.baseUrl}/files/scheduled/${id}`, { headers: this.getAuthHeaders() });
    }

    // Trash Management Methods
    getTrashedFiles(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/files/trash`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    restoreFromTrash(id: string): Observable<any> {
        return this.http.post(`${this.baseUrl}/files/trash/${id}/restore`, {}, { headers: this.getAuthHeaders() });
    }

    permanentlyDeleteFromTrash(id: string): Observable<any> {
        return this.http.delete(`${this.baseUrl}/files/trash/${id}/permanent`, { headers: this.getAuthHeaders() });
    }

    getTrashStats(): Observable<any> {
        return this.http.get<any>(`${this.baseUrl}/files/trash/stats`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    forceCleanupTrash(): Observable<any> {
        return this.http.post(`${this.baseUrl}/files/trash/cleanup`, {}, { headers: this.getAuthHeaders() });
    }
}
