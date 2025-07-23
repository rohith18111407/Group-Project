import { HttpClient, HttpHeaders } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { map, Observable } from "rxjs";

@Injectable({ providedIn: 'root' })
export class StaffService {
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

    getAllUsers(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/Users?pageNumber=1&pageSize=100`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
    }

    getAllFiles(): Observable<any[]> {
        return this.http.get<any>(`${this.baseUrl}/files`, { headers: this.getAuthHeaders() })
            .pipe(map(res => res.data));
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
}
